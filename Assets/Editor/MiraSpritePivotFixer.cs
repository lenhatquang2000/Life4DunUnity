using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public class MiraSpritePivotFixer : EditorWindow
{
    [MenuItem("Tools/Fix Mira Sprite Pivots")]
    public static void FixPivots()
    {
        string path = "Assets/Character/Mira";
        if (!Directory.Exists(path))
        {
            Debug.LogError($"Không tìm thấy đường dẫn: {path}");
            return;
        }

        // Lấy tất cả các file .meta trong thư mục Mira và các thư mục con
        string[] metaFiles = Directory.GetFiles(path, "*.png.meta", SearchOption.AllDirectories);
        int count = 0;

        foreach (string metaPath in metaFiles)
        {
            try
            {
                string content = File.ReadAllText(metaPath);
                string newContent = content;

                // 1. Đổi alignment thành 7 (BottomCenter)
                newContent = Regex.Replace(newContent, @"alignment:\s*\d+", "alignment: 7");

                // 2. Đổi spritePivot thành {x: 0.5, y: 0}
                newContent = Regex.Replace(newContent, @"spritePivot:\s*\{x:\s*[0-9\.]+, y:\s*[0-9\.]+\}", "spritePivot: {x: 0.5, y: 0}");

                // 3. Đổi pivot con thành {x: 0.5, y: 0}
                newContent = Regex.Replace(newContent, @"pivot:\s*\{x:\s*[0-9\.]+, y:\s*[0-9\.]+\}", "pivot: {x: 0.5, y: 0}");

                if (newContent != content)
                {
                    File.WriteAllText(metaPath, newContent);
                    count++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Lỗi khi xử lý file {metaPath}: {e.Message}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[MiraSpritePivotFixer] Đã cập nhật trực tiếp file .meta của {count} ảnh về Bottom-Center!");
    }
}
