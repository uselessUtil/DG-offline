package jp.gungho.dg.offline106;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.widget.Toast;
import java.io.*;
import java.security.MessageDigest;
import org.json.JSONObject;

/** User-selected documents only. No storage, network, or root permissions. */
public final class SaveTransferActivity extends Activity {
    private static final int REQUEST = 4607, LIMIT = 4 * 1024 * 1024;
    private static volatile String result = "";
    private static volatile boolean busy;
    private String stage="picker",exportSource="export.json";
    public static String directory(Activity a) {
        File d = new File(a.getFilesDir(), "dg-save-transfer");
        if (!d.exists() && !d.mkdirs()) throw new IllegalStateException("Cannot create backup folder");
        return d.getAbsolutePath();
    }
    public static synchronized String poll() { String r=result; result=""; return r; }
    public static boolean isBusy() { return busy; }
    private static synchronized void publish(Activity a,String value) {
        result=value;busy=false;
        try {
            File temporary=new File(directory(a),"result.tmp"),target=new File(directory(a),"result.txt");
            try(OutputStream out=new FileOutputStream(temporary)){out.write(value.getBytes("UTF-8"));out.flush();}
            if(!temporary.renameTo(target))throw new IOException("Cannot commit result");
        } catch(Exception ignored) { /* in-process poll remains available */ }
    }
    public static void message(final Activity a, final String text) {
        a.runOnUiThread(new Runnable(){ public void run(){ Toast.makeText(a,text,Toast.LENGTH_LONG).show(); }});
    }
    public static void open(final Activity a, final boolean exporting) {
        if (busy) return;
        busy=true; result="";new File(directory(a),"result.txt").delete();
        a.runOnUiThread(new Runnable(){ public void run(){
            try {
                if (Build.VERSION.SDK_INT < 19) throw new IllegalStateException("Save files require Android 4.4 or newer");
                Intent i=new Intent(a,SaveTransferActivity.class);i.putExtra("export",exporting);a.startActivity(i);
            } catch(Exception e) { publish(a,"error:"+e.getMessage()); }
        }});
    }
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        busy=true;
        if(state!=null) {
            stage=state.getString("stage");exportSource=state.getString("source");
            try { if("confirm".equals(stage))confirmImport();else if("choose".equals(stage))chooseExport(); }
            catch(Exception e){complete("error:"+e.getMessage());}return;
        }
        if(getIntent().getBooleanExtra("export",false) && latestRecovery()!=null)chooseExport();else startPicker();
    }
    protected void onSaveInstanceState(Bundle state) {
        state.putString("stage",stage);state.putString("source",exportSource);super.onSaveInstanceState(state);
    }
    private File latestRecovery() {
        File[] files=new File(directory(this)).listFiles();File latest=null;
        if(files!=null)for(File f:files)if(f.getName().startsWith("before-import-") && f.getName().endsWith(".json") &&
            (latest==null || f.lastModified()>latest.lastModified()))latest=f;
        return latest;
    }
    private void chooseExport() {
        stage="choose";
        new AlertDialog.Builder(this).setTitle("Export save")
            .setItems(new String[]{"Current save","Before last import (recovery)"},new DialogInterface.OnClickListener(){
                public void onClick(DialogInterface d,int which){
                    File recovery=latestRecovery();exportSource=which==1 && recovery!=null?recovery.getName():"export.json";startPicker();
                }
            }).setOnCancelListener(new DialogInterface.OnCancelListener(){
                public void onCancel(DialogInterface d){complete("cancelled");}
            }).show();
    }
    private void startPicker() {
        stage="picker";
        try {
            Intent i=new Intent(getIntent().getBooleanExtra("export",false) ?
                "android.intent.action.CREATE_DOCUMENT" : "android.intent.action.OPEN_DOCUMENT");
            i.addCategory(Intent.CATEGORY_OPENABLE);i.setType("application/json");
            if(getIntent().getBooleanExtra("export",false))
                i.putExtra(Intent.EXTRA_TITLE,"DivineGate-1.0.6-"+System.currentTimeMillis()+".dgbackup.json");
            startActivityForResult(i,REQUEST);
        } catch(Exception e) { complete("error:"+e.getMessage()); }
    }
    private void complete(String value) { publish(this,value);finish(); }
    public void onBackPressed() { complete("cancelled"); }
    private File local(String name) { return new File(directory(this),name); }
    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer=new byte[16384];int n,total=0;
        while((n=in.read(buffer))!=-1) {
            total+=n;if(total>LIMIT)throw new IOException("Backup exceeds 4 MiB");out.write(buffer,0,n);
        }
        out.flush();
    }
    protected void onActivityResult(int request,int code,Intent data) {
        super.onActivityResult(request,code,data);
        if(request!=REQUEST)return;
        if(code!=RESULT_OK || data==null || data.getData()==null){complete("cancelled");return;}
        Uri uri=data.getData();
        try {
            if(getIntent().getBooleanExtra("export",false)) {
                try(InputStream in=new FileInputStream(local(exportSource));
                    OutputStream out=getContentResolver().openOutputStream(uri,"wt")) {
                    if(out==null)throw new IOException("Cannot write selected document");copy(in,out);
                }
                complete("exported");return;
            }
            File pending=local("import.json");
            try(InputStream in=getContentResolver().openInputStream(uri);OutputStream out=new FileOutputStream(pending)) {
                if(in==null)throw new IOException("Cannot read selected document");copy(in,out);
            }
            stage="confirm";confirmImport();
        } catch(Exception e) { local("import.json").delete();complete("error:"+e.getMessage()); }
    }
    private void confirmImport() throws Exception {
            ByteArrayOutputStream bytes=new ByteArrayOutputStream();
            try(InputStream in=new FileInputStream(local("import.json"))){copy(in,bytes);}
            String summary=preview(bytes.toString("UTF-8"));
            new AlertDialog.Builder(this).setTitle("Import Divine Gate save")
                .setMessage(summary).setPositiveButton("Import and close game",new DialogInterface.OnClickListener(){
                    public void onClick(DialogInterface dialog,int which){complete("import-confirmed");}
                }).setNegativeButton("Cancel",new DialogInterface.OnClickListener(){
                    public void onClick(DialogInterface dialog,int which){complete("cancelled");}
                }).setOnCancelListener(new DialogInterface.OnCancelListener(){
                    public void onCancel(DialogInterface dialog){complete("cancelled");}
                }).show();
    }
    public static String preview(String text) throws Exception {
            if(text.getBytes("UTF-8").length>LIMIT)throw new IOException("Backup exceeds 4 MiB");
            JSONObject root=new JSONObject(text);
            if(!"dg-offline-save".equals(root.getString("format")) || root.getInt("schema")!=1 ||
                !"1.0.6".equals(root.getString("game_version")))throw new IOException("Not a compatible 1.0.6 backup");
            String payload=root.getString("payload");byte[] digest=MessageDigest.getInstance("SHA-256").digest(payload.getBytes("UTF-8"));
            StringBuilder hex=new StringBuilder();for(byte b:digest)hex.append(String.format("%02x",b&255));
            if(!hex.toString().equals(root.getString("sha256")))throw new IOException("Backup checksum mismatch");
            JSONObject save=new JSONObject(new JSONObject(payload).getString("server_save"));
            if(save.optLong("quest_id",0)!=0 || save.optBoolean("evolve_quest_active",false))throw new IOException("In-battle backups are not supported");
            int count=save.isNull("unit_list")?0:save.getJSONArray("unit_list").length();
            return "Name: "+save.getString("user_name")+"\nRank: "+save.getLong("player_rank")+
                "\nUnits: "+count+"\nBackup: "+root.getString("created_utc")+
                "\n\nReplace the current account? A recovery backup will be saved first. The game will close after import; reopen it to continue.";
    }
}
