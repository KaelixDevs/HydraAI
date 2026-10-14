package dev.kaelix.hydra;

import android.app.*;
import android.content.*;
import android.database.Cursor;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.*;
import android.provider.OpenableColumns;
import android.view.*;
import android.widget.*;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.regex.*;

public final class MainActivity extends Activity {
    private HydraApplication app;
    private LinearLayout root, body, bubbles;
    private TextView status, streamed;
    private EditText prompt;
    private Button send;
    private ScrollView scroll;
    private String screen="chat", pendingExport="";
    private int rendered=-1;
    private static final int MODEL=1,EXPORT=2,BACKUP=3,ATTACH=4;
    private int dp(int n) {return Math.round(n*getResources().getDisplayMetrics().density);}
    @Override public void onCreate(Bundle state) {
        super.onCreate(state); app=(HydraApplication)getApplication();
        if(state!=null) {screen=state.getString("screen","chat"); pendingExport=state.getString("export","");}
        root=new LinearLayout(this); root.setOrientation(LinearLayout.VERTICAL); root.setBackgroundColor(Color.rgb(16,17,22));
        if(Build.VERSION.SDK_INT>=30) {getWindow().setDecorFitsSystemWindows(false);root.setOnApplyWindowInsetsListener((view,insets)->{android.graphics.Insets i=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.ime());view.setPadding(i.left,i.top,i.right,i.bottom);return insets;});}
        TextView brand=text("HYDRA",22); brand.setPadding(dp(16),dp(12),dp(16),dp(4)); brand.setTypeface(null,Typeface.BOLD);root.addView(brand);
        LinearLayout nav=new LinearLayout(this);
        addNav(nav,"New",()->{if(!app.busy){app.newChat();show("chat");}});addNav(nav,"Chats",()->show("history"));addNav(nav,"Files",()->show("files"));addNav(nav,"Settings",()->show("settings"));root.addView(nav);
        body=new LinearLayout(this);body.setOrientation(LinearLayout.VERTICAL);root.addView(body,new LinearLayout.LayoutParams(-1,0,1));
        status=text("",12);status.setPadding(dp(12),dp(6),dp(12),dp(8));root.addView(status);setContentView(root);show(screen);
        if(state!=null && prompt!=null)prompt.setText(state.getString("draft",""));
    }
    @Override protected void onStart(){super.onStart();app.observer=this::refresh;refresh();}
    @Override protected void onStop(){app.observer=null;if(app.busy)app.stop();super.onStop();}
    @Override protected void onSaveInstanceState(Bundle out){out.putString("screen",screen);out.putString("export",pendingExport);if(prompt!=null)out.putString("draft",prompt.getText().toString());super.onSaveInstanceState(out);}
    private void addNav(LinearLayout row,String title,Runnable action){Button b=button(title,action);b.setTextSize(12);row.addView(b,new LinearLayout.LayoutParams(0,dp(48),1));}
    private TextView text(String value,int size){TextView t=new TextView(this);t.setText(value);t.setTextSize(size);t.setTextColor(Color.rgb(237,236,245));t.setPadding(dp(4),dp(4),dp(4),dp(4));return t;}
    private Button button(String name,Runnable action){Button b=new Button(this);b.setText(name);b.setAllCaps(false);b.setMinWidth(0);b.setMinimumWidth(0);b.setMinHeight(dp(48));b.setContentDescription(name);b.setOnClickListener(v->action.run());return b;}
    private void show(String page){if(app.busy&&!page.equals("chat")){app.status="Stop generation before changing pages.";refresh();return;}screen=page;body.removeAllViews();prompt=null;
        switch(page){case "history":history();break;case "files":files();break;case "settings":settings();break;default:chat();break;}refresh();}
    private LinearLayout list(String title){ScrollView s=new ScrollView(this);LinearLayout p=new LinearLayout(this);p.setOrientation(LinearLayout.VERTICAL);p.setPadding(dp(12),dp(8),dp(12),dp(12));p.addView(text(title,24));s.addView(p);body.addView(s,new LinearLayout.LayoutParams(-1,-1));return p;}
    private void chat(){
        scroll=new ScrollView(this);scroll.setFillViewport(true);bubbles=new LinearLayout(this);bubbles.setOrientation(LinearLayout.VERTICAL);bubbles.setPadding(dp(12),dp(8),dp(12),dp(8));scroll.addView(bubbles);body.addView(scroll,new LinearLayout.LayoutParams(-1,0,1));
        LinearLayout composer=new LinearLayout(this);composer.setGravity(Gravity.BOTTOM);composer.setPadding(dp(8),dp(6),dp(8),dp(8));
        Button options=button("⋯",()->{});options.setContentDescription("Model and conversation options");options.setOnClickListener(v->options(options));composer.addView(options,new LinearLayout.LayoutParams(dp(48),dp(48)));
        prompt=new EditText(this);prompt.setHint("Message Hydra…");prompt.setTextColor(Color.WHITE);prompt.setHintTextColor(0xff9995aa);prompt.setMinHeight(dp(48));prompt.setMaxLines(4);prompt.setTextSize(16);prompt.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_MULTI_LINE|android.text.InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);composer.addView(prompt,new LinearLayout.LayoutParams(0,-2,1));
        send=button("↑",()->{if(app.busy){app.stop();return;}String draft=prompt.getText().toString();app.send(draft);if(app.busy)prompt.setText("");});send.setContentDescription("Send message or stop generation");composer.addView(send,new LinearLayout.LayoutParams(dp(52),dp(48)));body.addView(composer);rendered=-1;renderMessages();
    }
    private void options(View anchor){PopupMenu menu=new PopupMenu(this,anchor);SubMenu profiles=menu.getMenu().addSubMenu("Profile: "+app.profile);
        for(String p:HydraApplication.PROFILES)profiles.add(p).setOnMenuItemClickListener(item->{app.profile=p;return true;});
        menu.getMenu().add("Load model / Settings").setOnMenuItemClickListener(i->{show("settings");return true;});
        menu.getMenu().add("Attach text file").setOnMenuItemClickListener(i->{if(!app.busy)pick(ATTACH,"text/*");return true;});
        menu.getMenu().add("Saved files").setOnMenuItemClickListener(i->{show("files");return true;});menu.show();}
    private void renderMessages(){if(!screen.equals("chat")||bubbles==null)return;JSONArray list=app.messages();if(rendered==list.length()&&app.busy){if(streamed!=null)streamed.setText(app.streaming);return;}
        bubbles.removeAllViews();rendered=list.length();if(list.length()==0)bubbles.addView(text("A place for every idea. Set up the included starter model in Settings, or import your own GGUF.",20));
        for(int i=0;i<list.length();i++){JSONObject message=list.optJSONObject(i);boolean user=message.optString("role").equals("user");LinearLayout card=new LinearLayout(this);card.setOrientation(LinearLayout.VERTICAL);card.setPadding(dp(12),dp(10),dp(12),dp(12));GradientDrawable bg=new GradientDrawable();bg.setColor(user?0xff272334:0xff1c1d27);bg.setCornerRadius(dp(12));card.setBackground(bg);LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,-2);lp.bottomMargin=dp(12);bubbles.addView(card,lp);
            card.addView(text(user?"You":message.optString("backend","Hydra"),11));String content=message.optString("text");TextView words=text(content,16);words.setTextIsSelectable(true);card.addView(words);if(!user)addOutputs(card,content);
            if(!message.optString("state","complete").equals("complete"))card.addView(text("Response "+message.optString("state"),12));}
        streamed=text(app.streaming,16);if(app.busy)bubbles.addView(streamed);scroll.post(()->scroll.fullScroll(View.FOCUS_DOWN));}
    private void refresh(){if(status==null)return;status.setText(app.status);if(send!=null)send.setText(app.busy?"■":"↑");if(screen.equals("chat"))renderMessages();}
    private void history(){LinearLayout p=list("Conversations");for(int i=app.history.length()-1;i>=0;i--){JSONObject c=app.history.optJSONObject(i);p.addView(button(c.optString("title"),()->{app.selected=c.optString("id");app.save();show("chat");}));}}
    private void settings(){LinearLayout p=list("Settings");p.addView(text("On-device models",20));p.addView(text(app.modelName()+"\n"+(app.model.exists()?String.format(Locale.ROOT,"%.0f MB on device",app.model.length()/1048576.0):"Not installed"),14));
        p.addView(text("The starter is SmolLM2-135M-Instruct / bartowski Q4_K_M (Apache-2.0). It is small and often inaccurate. Larger quantized models need more RAM. No internet permission is included in this app.",14));
        p.addView(button("Set up included starter",()->app.installModel(null,"SmolLM2-135M-Instruct (bartowski Q4_K_M)")));
        p.addView(button("Import GGUF from device",()->{if(!app.busy)pick(MODEL,"*/*");}));p.addView(button("Load model",()->app.load()));p.addView(button("Open chat",()->show("chat")));
        p.addView(text("Backups",20));p.addView(text("Choose an accessible Obsidian vault or backup folder. Saved outputs are copied there as separate files. Android does not run Git commits or desktop build tools.",14));
        p.addView(button("Connect backup / vault folder",()->startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE).addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION|Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION),BACKUP)));
        p.addView(button("Disconnect backup",()->{app.getSharedPreferences("hydra",0).edit().remove("backupTree").apply();app.status="Backup disconnected. Existing copies remain.";refresh();}));
        p.addView(text("Hydra AI 0.2.0 · Android 8+ · ARM64\nModels, chats and outputs stay in app storage. Uninstalling removes this local data; export files you want to keep. Generation stops when the app leaves the foreground.",13));}
    private void pick(int code,String mime){startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT).setType(mime).addCategory(Intent.CATEGORY_OPENABLE),code);}
    private String displayName(Uri uri){try(Cursor c=getContentResolver().query(uri,new String[]{OpenableColumns.DISPLAY_NAME},null,null,null)){if(c!=null&&c.moveToFirst())return c.getString(0);}return "Imported model";}
    @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(result!=RESULT_OK||data==null||data.getData()==null)return;Uri uri=data.getData();try{
        if(request==MODEL){app.installModel(uri,displayName(uri));}
        else if(request==BACKUP){if((data.getFlags()&Intent.FLAG_GRANT_WRITE_URI_PERMISSION)==0)throw new IOException("Choose a writable backup folder.");getContentResolver().takePersistableUriPermission(uri,Intent.FLAG_GRANT_WRITE_URI_PERMISSION);app.getSharedPreferences("hydra",0).edit().putString("backupTree",uri.toString()).apply();app.status="Backup folder connected.";}
        else if(request==EXPORT){File file=new File(app.outputs(),pendingExport);if(!file.getCanonicalFile().getParentFile().equals(app.outputs().getCanonicalFile()))throw new IOException("Invalid output path.");try(InputStream in=new FileInputStream(file);OutputStream out=getContentResolver().openOutputStream(uri,"wt")){if(out==null)throw new IOException("Cannot save this file.");copy(in,out,Long.MAX_VALUE);}app.status="Exported "+pendingExport;pendingExport="";}
        else if(request==ATTACH){try(InputStream in=getContentResolver().openInputStream(uri);ByteArrayOutputStream out=new ByteArrayOutputStream()){if(in==null)throw new IOException("Cannot read attachment.");copy(in,out,64*1024);String reference=new String(out.toByteArray(),StandardCharsets.UTF_8);if(reference.indexOf(0)>=0)throw new IOException("Choose a text file.");if(prompt!=null)prompt.append("\nAttached reference (untrusted):\n"+reference);}}
    }catch(Exception e){app.status=e.getMessage();}refresh();}
    static void copy(InputStream in,OutputStream out,long limit)throws IOException{byte[] buffer=new byte[32768];long total=0;int n;while((n=in.read(buffer))!=-1){total+=n;if(total>limit)throw new IOException("File exceeds the allowed size.");out.write(buffer,0,n);}}
    private void addOutputs(LinearLayout card,String source){Matcher m=Pattern.compile("^```([^\\r\\n]*)\\r?\\n([\\s\\S]*?)^```[ \\t]*$",Pattern.MULTILINE).matcher(source);int count=0;
        while(m.find()&&++count<=20){String[] info=m.group(1).trim().split("\\s+");String language=info[0].toLowerCase(Locale.ROOT);String extension;
            switch(language){case "html":case "css":case "json":case "xml":case "svg":case "obj":extension=language;break;case "javascript":case "js":extension="js";break;case "python":case "py":extension="py";break;case "csharp":case "cs":extension="cs";break;case "cpp":case "c++":extension="cpp";break;case "lua":case "luau":extension="luau";break;default:extension="txt";}
            String name="generated-"+count+"."+extension;for(String field:info)if(field.startsWith("filename=")){String proposed=field.substring(9);if(proposed.matches("[A-Za-z0-9][A-Za-z0-9_.-]{0,100}")&&proposed.endsWith("."+extension))name=proposed;}
            final String filename=name,content=m.group(2);card.addView(button("Save "+filename,()->saveOutput(filename,content)));}}
    private void saveOutput(String name,String content){if(app.busy)return;app.busy=true;app.status="Saving file…";refresh();app.worker.execute(()->{File file=new File(app.outputs(),UUID.randomUUID().toString().substring(0,8)+"-"+name);try{try(FileOutputStream out=new FileOutputStream(file)){out.write(content.getBytes(StandardCharsets.UTF_8));}try{app.backup(file);app.update("Saved "+name);}catch(Exception e){app.update("Saved locally; backup failed: "+e.getMessage());}}catch(Exception e){app.update(e.getMessage());}finally{app.main.post(()->{app.busy=false;app.changed();});}});}
    private void files(){LinearLayout p=list("Saved files");File[] files=app.outputs().listFiles();if(files==null||files.length==0){p.addView(text("Use Save beneath a code block to keep an output here.",16));return;}Arrays.sort(files,Comparator.comparingLong(File::lastModified).reversed());for(File file:files)p.addView(button(file.getName(),()->{pendingExport=file.getName();startActivityForResult(new Intent(Intent.ACTION_CREATE_DOCUMENT).setType("application/octet-stream").addCategory(Intent.CATEGORY_OPENABLE).putExtra(Intent.EXTRA_TITLE,file.getName()),EXPORT);}));}
}
