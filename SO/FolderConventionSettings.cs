using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

/// <summary>
/// 특정 폴더에 대한 컨벤션 규칙 (최적화 버전)
/// </summary>
[Serializable]
public class FolderConvention
{
    [Header("📁 폴더 정보")]
    public string folderPath = "Assets/";
    public string displayName = "기본 폴더";
    public string description = "일반적인 에셋 폴더";
    public Color folderColor = Color.white;
    public string folderIcon = "📁";

    [Header("📝 네이밍 규칙")]
    public string prefix = "";
    public string suffix = "";
    public NamingStyle namingStyle = NamingStyle.PascalCase;
    public bool enforceNaming = true;
    
    [Header("🎯 특수 규칙")]
    public bool isProjectSpecific = false;
    public string projectPrefix = "";
    
    [Header("🔧 파일 타입 규칙")]
    public List<string> allowedExtensions = new List<string>();
    public bool autoCapitalize = true;
    public bool removeSpecialChars = true;
    public bool preserveNumbers = true;

    [Header("⚡ 자동 처리")]
    public bool autoApply = false;
    public bool createSubfolders = false;
    public List<string> autoSubfolders = new List<string>();
    
    // 성능 최적화를 위한 캐시
    [NonSerialized] private bool _isInitialized = false;
    [NonSerialized] private string _normalizedPath = "";

    /// <summary>
    /// 파일이 이 폴더 규칙에 해당하는지 확인 (최적화)
    /// </summary>
    public bool IsMatch(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(folderPath))
            return false;

        // 초기화
        if (!_isInitialized)
        {
            _normalizedPath = folderPath.Replace("\\", "/");
            _isInitialized = true;
        }

        var normalizedFilePath = filePath.Replace("\\", "/");
        return normalizedFilePath.StartsWith(_normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 파일명에 컨벤션 적용
    /// </summary>
    public string ApplyConvention(string originalName, string fileExtension = "")
    {
        if (string.IsNullOrEmpty(originalName)) return originalName;

        string result = originalName.Trim();

        // 특수 문자 제거
        if (removeSpecialChars)
        {
            if (preserveNumbers)
            {
                result = System.Text.RegularExpressions.Regex.Replace(result, @"[^\w\s\d-]", "");
            }
            else
            {
                result = System.Text.RegularExpressions.Regex.Replace(result, @"[^\w\s-]", "");
            }
        }

        // 네이밍 스타일 적용
        result = ApplyNamingStyle(result);

        // 프로젝트별 접두사 적용
        if (isProjectSpecific && !string.IsNullOrEmpty(projectPrefix))
        {
            result = projectPrefix + result;
        }

        // 일반 접두사/접미사 적용
        if (!string.IsNullOrEmpty(prefix) && !result.StartsWith(prefix))
        {
            result = prefix + result;
        }
        
        if (!string.IsNullOrEmpty(suffix) && !result.EndsWith(suffix))
        {
            result = result + suffix;
        }

        // 자동 대문자화
        if (autoCapitalize && result.Length > 0)
        {
            result = char.ToUpper(result[0]) + result.Substring(1);
        }

        return result;
    }

    private string ApplyNamingStyle(string text)
    {
        return NamingStyleHelper.ApplyStyle(text, namingStyle);
    }

    /// <summary>
    /// 컨벤션 위반 체크
    /// </summary>
    public List<string> ValidateFile(string fileName, string fileExtension)
    {
        var violations = new List<string>();

        if (!enforceNaming) return violations;

        // 확장자 체크
        if (allowedExtensions.Count > 0 && !allowedExtensions.Contains(fileExtension.ToLower()))
        {
            violations.Add($"허용되지 않은 파일 확장자: {fileExtension}");
        }

        // 네이밍 스타일 체크
        var expectedName = ApplyConvention(fileName);
        if (fileName != expectedName)
        {
            violations.Add($"네이밍 컨벤션 위반: '{fileName}' → '{expectedName}'");
        }

        return violations;
    }

    /// <summary>
    /// 하위 폴더 자동 생성
    /// </summary>
    public void CreateSubfoldersIfNeeded()
    {
        if (!createSubfolders || autoSubfolders.Count == 0) return;

        foreach (var subfolder in autoSubfolders)
        {
            var subfolderPath = Path.Combine(folderPath, subfolder).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(subfolderPath))
            {
                AssetDatabase.CreateFolder(folderPath, subfolder);
                Debug.Log($"✅ 하위 폴더 생성: {subfolderPath}");
            }
        }
    }
}

/// <summary>
/// 폴더별 컨벤션 설정 관리자 (최적화 버전)
/// </summary>
[CreateAssetMenu(menuName = "Asset Organizer/Folder Convention Settings", fileName = "FolderConventionSettings")]
public class FolderConventionSettings : ScriptableObject
{
    [Header("🎯 전역 설정")]
    [SerializeField] private bool enableFolderConventions = true;
    [SerializeField] private bool showFolderSelectionDialog = true;
    [SerializeField] private bool lazyLoadSubfolders = true; // 하위 폴더 지연 로딩

    [Header("📁 폴더별 컨벤션")]
    [SerializeField] private List<FolderConvention> folderConventions = new List<FolderConvention>();

    [Header("🚫 전역 제외 설정")]
    [SerializeField] private List<string> globalExcludeFolders = new List<string>
    {
        "Packages", "Library", "Temp", "Build", "Builds", "UserSettings", "Logs", ".git", ".svn"
    };

    [SerializeField] private List<string> globalExcludeExtensions = new List<string>
    {
        ".meta", ".tmp", ".log", ".bak", ".cache", ".db"
    };

    // 캐시
    private Dictionary<string, FolderConvention> _pathToConventionCache = new Dictionary<string, FolderConvention>();
    private bool _cacheValid = false;

    // ==================== Properties ====================
    public bool ShowFolderSelectionDialog => showFolderSelectionDialog;
    public bool LazyLoadSubfolders => lazyLoadSubfolders;
    public IReadOnlyList<FolderConvention> FolderConventions => folderConventions;

    // ==================== 초기화 ====================
    void OnEnable()
    {
        if (folderConventions == null || folderConventions.Count == 0)
        {
            InitializeDefaultConventions();
        }
        InvalidateCache();
    }

    void OnValidate()
    {
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        _cacheValid = false;
        _pathToConventionCache.Clear();
    }

    private void RebuildCache()
    {
        _pathToConventionCache.Clear();
        
        // 경로 길이 기준으로 정렬 (더 구체적인 경로가 우선)
        var sortedConventions = folderConventions
            .Where(c => c != null && !string.IsNullOrEmpty(c.folderPath))
            .OrderByDescending(c => c.folderPath.Length)
            .ToList();

        foreach (var convention in sortedConventions)
        {
            var normalizedPath = convention.folderPath.Replace("\\", "/");
            if (!_pathToConventionCache.ContainsKey(normalizedPath))
            {
                _pathToConventionCache[normalizedPath] = convention;
            }
        }

        _cacheValid = true;
    }

    /// <summary>
    /// 최상위 폴더만 스캔 (성능 최적화)
    /// </summary>
    [ContextMenu("최상위 폴더만 스캔")]
    public void ScanTopLevelFoldersOnly()
    {
        Debug.Log("🔍 최상위 폴더 스캔을 시작합니다...");

        try
        {
            // 기존 하위 폴더 컨벤션 제거
            folderConventions.RemoveAll(c => c.folderPath.Count(ch => ch == '/') > 1);

            // Assets 바로 아래 폴더만 찾기
            var topLevelFolders = AssetDatabase.GetSubFolders("Assets");
            
            foreach (var folderPath in topLevelFolders)
            {
                if (IsGloballyExcluded(folderPath)) continue;
                
                var existing = GetConventionForFolder(folderPath);
                if (existing == null)
                {
                    var convention = CreateTopLevelConvention(folderPath);
                    folderConventions.Add(convention);
                }
            }

            InvalidateCache();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"✅ 최상위 폴더 스캔 완료! {topLevelFolders.Length}개 폴더 처리됨");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 폴더 스캔 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 특정 폴더의 하위 폴더만 스캔 (지연 로딩)
    /// </summary>
    public void ScanSubfoldersOf(string parentPath)
    {
        if (string.IsNullOrEmpty(parentPath)) return;

        try
        {
            var subfolders = AssetDatabase.GetSubFolders(parentPath);
            int addedCount = 0;

            foreach (var subfolderPath in subfolders)
            {
                if (IsGloballyExcluded(subfolderPath)) continue;
                
                var existing = GetConventionForFolder(subfolderPath);
                if (existing == null)
                {
                    var convention = CreateSubfolderConvention(subfolderPath, parentPath);
                    folderConventions.Add(convention);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                InvalidateCache();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log($"✅ {parentPath}의 하위 폴더 {addedCount}개 추가됨");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 하위 폴더 스캔 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 최상위 폴더용 컨벤션 생성
    /// </summary>
    private FolderConvention CreateTopLevelConvention(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        var convention = new FolderConvention
        {
            folderPath = folderPath,
            displayName = folderName,
            description = $"{folderName} 폴더",
            enforceNaming = true,
            autoApply = false // 기본값: 자동 적용 OFF (안정성)
        };

        // 폴더명 기반 자동 설정
        ConfigureConventionByFolderName(convention, folderName.ToLower());

        return convention;
    }

    /// <summary>
    /// 하위 폴더용 컨벤션 생성
    /// </summary>
    private FolderConvention CreateSubfolderConvention(string folderPath, string parentPath)
    {
        var parentConvention = GetConventionForFolder(parentPath);
        var folderName = Path.GetFileName(folderPath);
        
        var convention = new FolderConvention
        {
            folderPath = folderPath,
            displayName = folderName,
            description = $"{Path.GetFileName(parentPath)}의 하위 폴더",
            enforceNaming = parentConvention?.enforceNaming ?? true,
            autoApply = false
        };

        // 부모 폴더의 설정 일부 상속
        if (parentConvention != null)
        {
            convention.namingStyle = parentConvention.namingStyle;
            convention.removeSpecialChars = parentConvention.removeSpecialChars;
            convention.preserveNumbers = parentConvention.preserveNumbers;
        }

        ConfigureConventionByFolderName(convention, folderName.ToLower());

        return convention;
    }

    /// <summary>
    /// 폴더명에 따른 컨벤션 자동 설정
    /// </summary>
    private void ConfigureConventionByFolderName(FolderConvention convention, string lowerFolderName)
    {
        var presets = GetFolderPresets();
        
        foreach (var preset in presets)
        {
            if (preset.keywords.Any(keyword => lowerFolderName.Contains(keyword)))
            {
                preset.ApplyTo(convention);
                return;
            }
        }

        // 기본값
        convention.folderIcon = "📁";
        convention.folderColor = Color.white;
    }

    /// <summary>
    /// 폴더 프리셋 정의
    /// </summary>
    private List<FolderPreset> GetFolderPresets()
    {
        return new List<FolderPreset>
        {
            new FolderPreset(
                new[] { "texture", "textures", "sprite", "sprites", "image", "images" },
                "🖼️", new Color(0.9f, 0.5f, 0.3f, 1f), "T_",
                new[] { ".png", ".jpg", ".tga", ".psd", ".tif" }
            ),
            new FolderPreset(
                new[] { "ui", "gui", "hud", "interface" },
                "🖥️", new Color(0.3f, 0.7f, 1f, 1f), "UI_",
                new[] { ".png", ".jpg", ".prefab" }
            ),
            new FolderPreset(
                new[] { "audio", "sound", "music", "sfx" },
                "🎵", new Color(0.8f, 0.3f, 0.8f, 1f), "Audio_",
                new[] { ".wav", ".mp3", ".ogg", ".m4a" }
            ),
            new FolderPreset(
                new[] { "model", "models", "mesh", "meshes", "3d" },
                "🗿", new Color(0.6f, 0.6f, 0.6f, 1f), "M_",
                new[] { ".fbx", ".obj", ".dae", ".3ds", ".blend" }
            ),
            new FolderPreset(
                new[] { "material", "materials", "mat", "mats" },
                "🎨", new Color(0.7f, 0.4f, 0.8f, 1f), "Mat_",
                new[] { ".mat" }
            ),
            new FolderPreset(
                new[] { "prefab", "prefabs", "template", "templates" },
                "📦", new Color(0.4f, 0.8f, 0.4f, 1f), "P_",
                new[] { ".prefab" }
            ),
            new FolderPreset(
                new[] { "animation", "animations", "anim", "anims" },
                "🎬", new Color(0.7f, 0.9f, 0.3f, 1f), "Anim_",
                new[] { ".anim", ".controller" }
            ),
            new FolderPreset(
                new[] { "vfx", "effect", "effects", "particle", "particles" },
                "💫", new Color(1f, 0.5f, 0.8f, 1f), "VFX_",
                new[] { ".prefab", ".mat", ".png" }
            ),
            new FolderPreset(
                new[] { "shader", "shaders", "hlsl", "glsl" },
                "✨", new Color(1f, 0.8f, 0.2f, 1f), "S_",
                new[] { ".shader", ".hlsl", ".glsl", ".cginc" }
            ),
            new FolderPreset(
                new[] { "scene", "scenes", "level", "levels" },
                "🎮", new Color(0.5f, 0.8f, 1f, 1f), "Scene_",
                new[] { ".unity" }
            )
        };
    }

    /// <summary>
    /// 폴더 등록 (존재하지 않을 때만)
    /// </summary>
    public void TryRegister(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        if (GetConventionForFolder(folderPath) != null) return;

        var convention = new FolderConvention
        {
            folderPath = folderPath,
            displayName = Path.GetFileName(folderPath),
            description = $"{folderPath} 자동 등록",
            autoApply = false
        };

        AddFolderConvention(convention);
        Debug.Log($"[AssetOrganizer] ➕ 폴더 컨벤션 등록: {folderPath}");
    }

    /// <summary>
    /// 파일 경로에 맞는 폴더 컨벤션 찾기 (캐시 사용)
    /// </summary>
    public FolderConvention FindConventionForPath(string filePath)
    {
        if (!enableFolderConventions || string.IsNullOrEmpty(filePath))
            return null;

        if (!_cacheValid)
        {
            RebuildCache();
        }

        var normalizedPath = filePath.Replace("\\", "/");
        
        // 파일이 속한 폴더 찾기
        var directory = Path.GetDirectoryName(normalizedPath)?.Replace("\\", "/");
        
        while (!string.IsNullOrEmpty(directory) && directory.StartsWith("Assets"))
        {
            if (_pathToConventionCache.TryGetValue(directory, out var convention))
            {
                return convention;
            }
            
            // 상위 폴더로 이동
            directory = Path.GetDirectoryName(directory)?.Replace("\\", "/");
        }

        return null;
    }

    /// <summary>
    /// 특정 폴더의 컨벤션 가져오기
    /// </summary>
    public FolderConvention GetConventionForFolder(string folderPath)
    {
        return folderConventions.FirstOrDefault(c => c.folderPath == folderPath);
    }

    /// <summary>
    /// 폴더 컨벤션 추가
    /// </summary>
    public void AddFolderConvention(FolderConvention convention)
    {
        if (convention != null && !folderConventions.Any(c => c.folderPath == convention.folderPath))
        {
            folderConventions.Add(convention);
            InvalidateCache();
        }
    }

    /// <summary>
    /// 폴더 컨벤션 제거
    /// </summary>
    public void RemoveFolderConvention(string folderPath)
    {
        folderConventions.RemoveAll(c => c.folderPath == folderPath);
        InvalidateCache();
    }

    /// <summary>
    /// 파일이 전역 제외 대상인지 확인
    /// </summary>
    public bool IsGloballyExcluded(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return true;

        // 제외 폴더 체크
        foreach (var excludeFolder in globalExcludeFolders)
        {
            if (filePath.Contains($"/{excludeFolder}/") || 
                filePath.Contains($"/{excludeFolder}\\") ||
                filePath.EndsWith($"/{excludeFolder}") ||
                filePath.StartsWith($"{excludeFolder}/"))
                return true;
        }

        // 제외 확장자 체크
        var extension = Path.GetExtension(filePath);
        return globalExcludeExtensions.Contains(extension);
    }

    [ContextMenu("기본 폴더 컨벤션 생성")]
    public void InitializeDefaultConventions()
    {
        folderConventions = new List<FolderConvention>();

        // 기본 최상위 폴더들만 추가
        var defaultFolders = new[]
        {
            ("Assets/Textures", "텍스처", "🖼️", "T_"),
            ("Assets/Materials", "머티리얼", "🎨", "Mat_"),
            ("Assets/Models", "3D 모델", "🗿", "M_"),
            ("Assets/Audio", "오디오", "🎵", "Audio_"),
            ("Assets/Prefabs", "프리팹", "📦", "P_"),
            ("Assets/UI", "UI", "🖥️", "UI_"),
            ("Assets/Animations", "애니메이션", "🎬", "Anim_"),
            ("Assets/VFX", "시각 효과", "💫", "VFX_"),
            ("Assets/Scenes", "씬", "🎮", "Scene_")
        };

        foreach (var (path, name, icon, prefix) in defaultFolders)
        {
            var convention = new FolderConvention
            {
                folderPath = path,
                displayName = name,
                folderIcon = icon,
                prefix = prefix,
                namingStyle = NamingStyle.PascalCase,
                enforceNaming = true,
                autoApply = false
            };
            
            ConfigureConventionByFolderName(convention, name.ToLower());
            folderConventions.Add(convention);
        }

        InvalidateCache();
    }
}

/// <summary>
/// 폴더 프리셋 헬퍼 클래스
/// </summary>
public class FolderPreset
{
    public string[] keywords;
    public string icon;
    public Color color;
    public string prefix;
    public string[] extensions;

    public FolderPreset(string[] keywords, string icon, Color color, string prefix, string[] extensions)
    {
        this.keywords = keywords;
        this.icon = icon;
        this.color = color;
        this.prefix = prefix;
        this.extensions = extensions;
    }

    public void ApplyTo(FolderConvention convention)
    {
        convention.folderIcon = icon;
        convention.folderColor = color;
        convention.prefix = prefix;
        convention.allowedExtensions = new List<string>(extensions);
    }
}

/// <summary>
/// 네이밍 스타일 헬퍼
/// </summary>
public static class NamingStyleHelper
{
    public static string ApplyStyle(string text, NamingStyle style)
    {
        return style switch
        {
            NamingStyle.PascalCase => ToPascalCase(text),
            NamingStyle.CamelCase => ToCamelCase(text),
            NamingStyle.SnakeCase => ToSnakeCase(text),
            NamingStyle.KebabCase => ToKebabCase(text),
            NamingStyle.UpperCase => text.ToUpper(),
            NamingStyle.LowerCase => text.ToLower(),
            _ => text
        };
    }

    private static string ToPascalCase(string text)
    {
        var words = text.Split(new char[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
        }
        return string.Join("", words);
    }

    private static string ToCamelCase(string text)
    {
        string pascal = ToPascalCase(text);
        return pascal.Length > 0 ? char.ToLower(pascal[0]) + pascal.Substring(1) : pascal;
    }

    private static string ToSnakeCase(string text)
    {
        return System.Text.RegularExpressions.Regex
            .Replace(text, "(?<!^)([A-Z])", "_$1")
            .Replace(' ', '_')
            .Replace('-', '_')
            .ToLower();
    }

    private static string ToKebabCase(string text)
    {
        return ToSnakeCase(text).Replace('_', '-');
    }
}