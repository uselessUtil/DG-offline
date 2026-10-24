import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;
import jp.gungho.dg.offline106.SaveTransferActivity;
public final class SavePreviewTest {
    public static void main(String[] args) throws Exception {
        String good=new String(Files.readAllBytes(Paths.get(args[0])),StandardCharsets.UTF_8);
        String summary=SaveTransferActivity.preview(good);
        if(!summary.contains("Rank: 250") || !summary.contains("備份測試"))throw new Exception("Wrong account preview");
        JSONObject bad=new JSONObject(good);bad.put("sha256","wrong");boolean rejected=false;
        try{SaveTransferActivity.preview(bad.toString());}catch(Exception e){rejected=true;}
        if(!rejected)throw new Exception("Native preview accepted bad checksum");
        bad=new JSONObject(good);bad.put("game_version","2.1.0");rejected=false;
        try{SaveTransferActivity.preview(bad.toString());}catch(Exception e){rejected=true;}
        if(!rejected)throw new Exception("Native preview accepted foreign version");
        System.out.println("PASS native Java protocol: exported C# backup verifies with Java SHA-256/JSON, Unicode account summary matches; damaged/foreign backups rejected before confirmation. Android picker/dialog lifecycle not executed.");
    }
}
