using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 에셋 임포트 다이얼로그 (직관적 UI 버전)
/// </summary>
public class AssetImportDialog : EditorWindow
{
    // ==================== 상수 정의 ====================
    private const float WINDOW_WIDTH = 900f;
    private const float WINDOW_HEIGHT = 600f;
    private const float FOLDER_ITEM_HEIGHT = 32f;
    private const float FOLDER_INDENT = 20f;
    
    // ==================== 데이터 ====================
    private List<string> importedAssetPaths = new List<string>();
    private FolderConventionSettings folderSettings;
    private Vector2 folderScrollPosition;
    private Vector2 assetScrollPosition;
    
    private string selectedFolderPath = "";
    private FolderConvention selectedConvention;
    
    // UI 상태
    private bool showAdvancedOptions = false;
    private string quickSearchText = "";
    
    // 네이밍 설정
    private string customPrefix = "";
    private string customSuffix = "";
    private NamingStyle customNamingStyle = NamingStyle.AsIs;
    
    // 폴더 트리 데이터
    private class FolderNode
    {
        public string path;
        public string name;
        public List<FolderNode> children = new List<FolderNode>();
        public bool isExpanded = false;
        public bool isLoaded = false; // 하위 폴더 로드 여부
        public int depth = 0;
        public FolderConvention convention;
    }
    
    private FolderNode rootFolder;
    private Dictionary<string, FolderNode> folderNodeCache = new Dictionary<string, FolderNode>();
    
    private bool isProcessing = false;
    private float processingProgress = 0f;
    private List<AssetProcessingResult> processingResults = new List<AssetProcessingResult>();

    /// <summary>
    /// 에셋 리스트로 다이얼로그 열기
    /// </summary>
    public static void ShowForAssets(List<string> assetPaths)
    {
        if (assetPaths == null || assetPaths.Count == 0) return;

        var window = GetWindow<AssetImportDialog>(true, "📁 새 에셋 정리하기", true);
        window.minSize = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
        window.maxSize = new Vector2(1200, 800);
        window.importedAssetPaths = assetPaths;
        window.Initialize();
        
        // 화면 중앙에 위치
        var mainWindow = EditorGUIUtility.GetMainWindowPosition();
        var center = mainWindow.center;
        window.position = new Rect(center.x - WINDOW_WIDTH/2, center.y - WINDOW_HEIGHT/2, WINDOW_WIDTH, WINDOW_HEIGHT);
        
        window.Show();
    }

    private void Initialize()
    {
        // 설정 로드
        folderSettings = Resources.Load<FolderConventionSettings>("FolderConventionSettings");
        if (folderSettings == null)
        {
            CreateDefaultFolderSettings();
        }

        // 최상위 폴더만 로드 (성능 최적화)
        BuildTopLevelFolderTree();
        
        // 튜토리얼 체크
        bool skipTutorial = EditorPrefs.GetBool("AssetOrganizer_SkipSimpleTutorial", false);
        if (!skipTutorial && importedAssetPaths.Count > 10)
        {
            ShowSimpleTutorial();
        }
    }

    private void BuildTopLevelFolderTree()
    {
        rootFolder = new FolderNode
        {
            path = "Assets",
            name = "Assets",
            depth = 0,
            isExpanded = true,
            isLoaded = true
        };
        
        folderNodeCache.Clear();
        folderNodeCache[rootFolder.path] = rootFolder;
        
        // 최상위 폴더만 로드
        LoadSubfolders(rootFolder, false);
    }

    private void LoadSubfolders(FolderNode parent, bool recursive)
    {
        if (parent.isLoaded && !recursive) return;
        
        try
        {
            var subfolders = AssetDatabase.GetSubFolders(parent.path);
            parent.children.Clear();
            
            foreach (var subfolder in subfolders)
            {
                var folderName = Path.GetFileName(subfolder);
                if (folderSettings != null && folderSettings.IsGloballyExcluded(subfolder))
                    continue;
                
                var node = new FolderNode
                {
                    path = subfolder,
                    name = folderName,
                    depth = parent.depth + 1,
                    isExpanded = false,
                    isLoaded = false,
                    convention = folderSettings?.GetConventionForFolder(subfolder)
                };
                
                parent.children.Add(node);
                folderNodeCache[subfolder] = node;
                
                if (recursive)
                {
                    LoadSubfolders(node, true);
                }
            }
            
            parent.isLoaded = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"폴더 로드 실패: {parent.path} - {e.Message}");
        }
    }

    private void OnGUI()
    {
        if (isProcessing)
        {
            DrawProcessingOverlay();
            return;
        }

        DrawHeader();
        DrawMainContent();
        DrawActionButtons();
    }

    private void DrawHeader()
    {
        // 헤더 배경
        var headerRect = new Rect(0, 0, position.width, 70);
        EditorGUI.DrawRect(headerRect, StyleHelper.Colors.Primary);
        
        GUILayout.BeginArea(new Rect(20, 10, position.width - 40, 50));
        
        EditorGUILayout.BeginHorizontal();
        
        // 타이틀
        var titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 24,
            normal = { textColor = Color.white }
        };
        GUILayout.Label($"📁 새 에셋 정리하기", titleStyle);
        
        GUILayout.FlexibleSpace();
        
        // 에셋 개수 표시
        var countStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleRight
        };
        GUILayout.Label($"{importedAssetPaths.Count}개 파일", countStyle);
        
        EditorGUILayout.EndHorizontal();
        
        // 간단한 설명
        var descStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
        };
        GUILayout.Label("폴더를 선택하고 네이밍 규칙을 설정하세요", descStyle);
        
        GUILayout.EndArea();
        
        GUILayout.Space(75);
    }

    private void DrawMainContent()
    {
        EditorGUILayout.BeginHorizontal();
        
        // 왼쪽: 에셋 리스트
        DrawAssetListPanel();
        
        // 중앙: 폴더 선택
        DrawFolderSelectionPanel();
        
        // 오른쪽: 설정
        DrawSettingsPanel();
        
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAssetListPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        
        // 패널 헤더
        DrawPanelHeader("📦 가져온 파일", StyleHelper.Colors.Info);
        
        // 검색창
        EditorGUILayout.BeginHorizontal();
        quickSearchText = EditorGUILayout.TextField(quickSearchText, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("✖", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            quickSearchText = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 에셋 리스트
        assetScrollPosition = EditorGUILayout.BeginScrollView(assetScrollPosition);
        
        var filteredAssets = string.IsNullOrEmpty(quickSearchText) 
            ? importedAssetPaths 
            : importedAssetPaths.Where(p => Path.GetFileName(p).ToLower().Contains(quickSearchText.ToLower())).ToList();
        
        foreach (var assetPath in filteredAssets)
        {
            DrawAssetItem(assetPath);
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawAssetItem(string assetPath)
    {
        var fileName = Path.GetFileName(assetPath);
        var icon = AssetDatabase.GetCachedIcon(assetPath);
        
        EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
        
        if (icon != null)
        {
            GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
        }
        
        // 파일명
        var labelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11
        };
        GUILayout.Label(fileName, labelStyle);
        
        EditorGUILayout.EndHorizontal();
        
        // 미리보기 (선택된 폴더가 있을 때)
        if (!string.IsNullOrEmpty(selectedFolderPath))
        {
            var preview = GetPreviewName(Path.GetFileNameWithoutExtension(assetPath));
            if (preview != Path.GetFileNameWithoutExtension(assetPath))
            {
                var previewStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = StyleHelper.Colors.Success },
                    padding = new RectOffset(22, 0, 0, 0)
                };
                GUILayout.Label($"→ {preview}", previewStyle);
            }
        }
    }

    private void DrawFolderSelectionPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        // 패널 헤더
        DrawPanelHeader("📂 대상 폴더 선택", StyleHelper.Colors.Primary);
        
        // 폴더 트리
        folderScrollPosition = EditorGUILayout.BeginScrollView(folderScrollPosition);
        
        if (rootFolder != null && rootFolder.children.Count > 0)
        {
            foreach (var child in rootFolder.children)
            {
                DrawFolderNode(child);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("폴더가 없습니다. 프로젝트 구조를 확인하세요.", MessageType.Warning);
        }
        
        EditorGUILayout.EndScrollView();
        
        // 선택된 폴더 정보
        if (!string.IsNullOrEmpty(selectedFolderPath))
        {
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(StyleHelper.CardStyle);
            
            var selectedStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };
            GUILayout.Label("✅ 선택된 폴더", selectedStyle);
            GUILayout.Label(selectedFolderPath, EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawFolderNode(FolderNode node)
    {
        var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(FOLDER_ITEM_HEIGHT));
        
        // 선택 하이라이트
        if (selectedFolderPath == node.path)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.6f, 1f, 0.2f));
        }
        
        // 들여쓰기
        GUILayout.Space(node.depth * FOLDER_INDENT);
        
        // 확장/축소 버튼
        bool hasSubfolders = AssetDatabase.GetSubFolders(node.path).Length > 0;
        if (hasSubfolders)
        {
            var foldoutContent = new GUIContent(node.isExpanded ? "▼" : "▶");
            if (GUILayout.Button(foldoutContent, GUILayout.Width(20)))
            {
                node.isExpanded = !node.isExpanded;
                if (node.isExpanded && !node.isLoaded)
                {
                    // 지연 로딩
                    LoadSubfolders(node, false);
                    
                    // 하위 폴더 컨벤션 스캔
                    if (folderSettings != null && folderSettings.LazyLoadSubfolders)
                    {
                        folderSettings.ScanSubfoldersOf(node.path);
                    }
                }
            }
        }
        else
        {
            GUILayout.Space(22);
        }
        
        // 폴더 아이콘과 이름
        var convention = node.convention ?? folderSettings?.FindConventionForPath(node.path);
        var folderIcon = convention?.folderIcon ?? "📁";
        var folderColor = convention?.folderColor ?? Color.white;
        
        var iconStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = folderColor }
        };
        GUILayout.Label(folderIcon, iconStyle, GUILayout.Width(25));
        
        // 폴더명 버튼
        var buttonStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = node.depth == 1 ? FontStyle.Bold : FontStyle.Normal,
            fontSize = node.depth == 1 ? 13 : 12,
            normal = { textColor = selectedFolderPath == node.path ? StyleHelper.Colors.Primary : StyleHelper.Colors.TextPrimary }
        };
        
        if (GUILayout.Button(node.name, buttonStyle))
        {
            SelectFolder(node);
        }
        
        // 컨벤션 표시
        if (convention != null && convention.enforceNaming)
        {
            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = StyleHelper.Colors.Success }
            };
            GUILayout.Label("✓", badgeStyle, GUILayout.Width(15));
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 하위 폴더 그리기
        if (node.isExpanded && node.children.Count > 0)
        {
            foreach (var child in node.children)
            {
                DrawFolderNode(child);
            }
        }
    }

    private void DrawSettingsPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(300));
        
        // 패널 헤더
        DrawPanelHeader("⚙️ 네이밍 설정", StyleHelper.Colors.Secondary);
        
        EditorGUILayout.BeginVertical(StyleHelper.CardStyle);
        
        if (selectedConvention != null)
        {
            // 폴더 컨벤션 정보
            var conventionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            GUILayout.Label("📋 폴더 기본 설정", conventionStyle);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"접두사: {selectedConvention.prefix}", EditorStyles.miniLabel);
            GUILayout.Label($"스타일: {selectedConvention.namingStyle}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
        }
        
        // 커스텀 설정
        var customStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        GUILayout.Label("✏️ 사용자 정의", customStyle);
        
        // 접두사
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("접두사:", GUILayout.Width(60));
        customPrefix = EditorGUILayout.TextField(customPrefix);
        EditorGUILayout.EndHorizontal();
        
        // 접미사
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("접미사:", GUILayout.Width(60));
        customSuffix = EditorGUILayout.TextField(customSuffix);
        EditorGUILayout.EndHorizontal();
        
        // 네이밍 스타일
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("스타일:", GUILayout.Width(60));
        customNamingStyle = (NamingStyle)EditorGUILayout.EnumPopup(customNamingStyle);
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 빠른 템플릿
        GUILayout.Label("⚡ 빠른 설정", customStyle);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("텍스처"))
        {
            customPrefix = "T_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        if (GUILayout.Button("UI"))
        {
            customPrefix = "UI_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        if (GUILayout.Button("음향"))
        {
            customPrefix = "Audio_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("모델"))
        {
            customPrefix = "M_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        if (GUILayout.Button("머티리얼"))
        {
            customPrefix = "Mat_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        if (GUILayout.Button("초기화"))
        {
            customPrefix = "";
            customSuffix = "";
            customNamingStyle = NamingStyle.AsIs;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // 고급 옵션
        GUILayout.Space(10);
        showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "고급 옵션");
        if (showAdvancedOptions)
        {
            EditorGUILayout.BeginVertical(StyleHelper.CardStyle);
            
            if (selectedConvention != null)
            {
                selectedConvention.removeSpecialChars = EditorGUILayout.Toggle("특수문자 제거", selectedConvention.removeSpecialChars);
                selectedConvention.preserveNumbers = EditorGUILayout.Toggle("숫자 보존", selectedConvention.preserveNumbers);
                selectedConvention.autoCapitalize = EditorGUILayout.Toggle("자동 대문자화", selectedConvention.autoCapitalize);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // 미리보기
        DrawPreviewSection();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewSection()
    {
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical(StyleHelper.CardStyle);
        
        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        GUILayout.Label("👁️ 미리보기", headerStyle);
        
        var examples = new[] { "texture_01", "UI-Button", "PlayerModel", "bgm_title" };
        
        foreach (var example in examples)
        {
            var preview = GetPreviewName(example);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(example, EditorStyles.miniLabel, GUILayout.Width(100));
            GUILayout.Label("→", GUILayout.Width(20));
            
            var previewStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = StyleHelper.Colors.Success }
            };
            GUILayout.Label(preview, previewStyle);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawActionButtons()
    {
        GUILayout.FlexibleSpace();
        
        // 하단 액션 바
        var buttonAreaHeight = 60f;
        var buttonRect = new Rect(0, position.height - buttonAreaHeight, position.width, buttonAreaHeight);
        EditorGUI.DrawRect(buttonRect, StyleHelper.Colors.BackgroundSecondary);
        
        GUILayout.BeginArea(new Rect(20, position.height - buttonAreaHeight + 10, position.width - 40, buttonAreaHeight - 20));
        EditorGUILayout.BeginHorizontal();
        
        // 좌측 정보
        var infoStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 12
        };
        
        var readyCount = string.IsNullOrEmpty(selectedFolderPath) ? 0 : importedAssetPaths.Count;
        GUILayout.Label($"📊 {readyCount}/{importedAssetPaths.Count} 파일 준비됨", infoStyle);
        
        GUILayout.FlexibleSpace();
        
        // 버튼들
        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 40
        };
        
        if (GUILayout.Button("❌ 취소", buttonStyle, GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("취소", "정말 취소하시겠습니까?", "예", "아니오"))
            {
                Close();
            }
        }
        
        GUI.enabled = true;
        
        if (GUILayout.Button("📍 현재 위치 유지", buttonStyle, GUILayout.Width(140)))
        {
            ApplyNamingOnly();
        }
        
        GUI.enabled = !string.IsNullOrEmpty(selectedFolderPath);
        GUI.backgroundColor = StyleHelper.Colors.Primary;
        
        if (GUILayout.Button("✨ 파일 정리하기", buttonStyle, GUILayout.Width(140)))
        {
            ApplyChanges();
        }
        
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawProcessingOverlay()
    {
        // 반투명 오버레이
        var overlayRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(overlayRect, new Color(0, 0, 0, 0.8f));
        
        // 중앙 프로세싱 박스
        var boxWidth = 400f;
        var boxHeight = 200f;
        var boxRect = new Rect(
            (position.width - boxWidth) / 2,
            (position.height - boxHeight) / 2,
            boxWidth,
            boxHeight
        );
        
        GUILayout.BeginArea(boxRect);
        EditorGUILayout.BeginVertical(StyleHelper.CardStyle);
        
        GUILayout.Space(20);
        
        var titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("📦 파일 정리 중...", titleStyle);
        
        GUILayout.Space(20);
        
        // 진행률 바
        var progressRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(progressRect, processingProgress, $"{(processingProgress * 100):F0}%");
        
        GUILayout.Space(10);
        
        // 현재 처리 중인 파일
        if (processingResults.Count > 0)
        {
            var lastResult = processingResults.Last();
            var fileStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"처리 중: {Path.GetFileName(lastResult.originalPath)}", fileStyle);
        }
        
        GUILayout.Space(20);
        
        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawPanelHeader(string title, Color color)
    {
        var headerRect = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, color * 0.3f);
        
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color }
        };
        
        EditorGUI.LabelField(headerRect, title, headerStyle);
    }

    private void SelectFolder(FolderNode node)
    {
        selectedFolderPath = node.path;
        selectedConvention = node.convention ?? folderSettings?.FindConventionForPath(node.path);
        
        // 컨벤션 설정 적용
        if (selectedConvention != null)
        {
            customPrefix = selectedConvention.prefix;
            customSuffix = selectedConvention.suffix;
            customNamingStyle = selectedConvention.namingStyle;
        }
        else
        {
            // 폴더명 기반 자동 설정
            AutoConfigureByFolderName(node.name);
        }
    }

    private void AutoConfigureByFolderName(string folderName)
    {
        var lower = folderName.ToLower();
        
        if (lower.Contains("texture") || lower.Contains("sprite"))
        {
            customPrefix = "T_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        else if (lower.Contains("ui") || lower.Contains("gui"))
        {
            customPrefix = "UI_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        else if (lower.Contains("audio") || lower.Contains("sound"))
        {
            customPrefix = "Audio_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        else if (lower.Contains("model") || lower.Contains("mesh"))
        {
            customPrefix = "M_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        else if (lower.Contains("material") || lower.Contains("mat"))
        {
            customPrefix = "Mat_";
            customNamingStyle = NamingStyle.PascalCase;
        }
        else
        {
            customPrefix = "";
            customNamingStyle = NamingStyle.PascalCase;
        }
    }

    private string GetPreviewName(string originalName)
    {
        var result = originalName;
        
        // 네이밍 스타일 적용
        if (customNamingStyle != NamingStyle.AsIs)
        {
            result = NamingStyleHelper.ApplyStyle(result, customNamingStyle);
        }
        
        // 접두사/접미사 적용
        if (!string.IsNullOrEmpty(customPrefix) && !result.StartsWith(customPrefix))
        {
            result = customPrefix + result;
        }
        
        if (!string.IsNullOrEmpty(customSuffix) && !result.EndsWith(customSuffix))
        {
            result = result + customSuffix;
        }
        
        return result;
    }

    private void ApplyNamingOnly()
    {
        isProcessing = true;
        processingProgress = 0f;
        processingResults.Clear();
        
        EditorApplication.delayCall += ProcessNamingOnly;
    }

    private void ApplyChanges()
    {
        if (string.IsNullOrEmpty(selectedFolderPath))
        {
            EditorUtility.DisplayDialog("오류", "대상 폴더를 선택하세요.", "확인");
            return;
        }
        
        isProcessing = true;
        processingProgress = 0f;
        processingResults.Clear();
        
        EditorApplication.delayCall += ProcessAssets;
    }

    private void ProcessNamingOnly()
    {
        int processedCount = 0;
        int totalCount = importedAssetPaths.Count;
        
        foreach (var assetPath in importedAssetPaths)
        {
            try
            {
                var directory = Path.GetDirectoryName(assetPath);
                var originalName = Path.GetFileNameWithoutExtension(assetPath);
                var extension = Path.GetExtension(assetPath);
                var newName = GetPreviewName(originalName);
                
                if (originalName != newName)
                {
                    var newPath = Path.Combine(directory, newName + extension).Replace("\\", "/");
                    var error = AssetDatabase.MoveAsset(assetPath, newPath);
                    
                    var result = new AssetProcessingResult(assetPath, newPath, string.IsNullOrEmpty(error), error);
                    processingResults.Add(result);
                    
                    if (result.success)
                    {
                        Debug.Log($"✅ 이름 변경: {originalName} → {newName}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 처리 실패: {assetPath} - {e.Message}");
            }
            
            processedCount++;
            processingProgress = (float)processedCount / totalCount;
        }
        
        FinishProcessing();
    }

    private void ProcessAssets()
    {
        int processedCount = 0;
        int totalCount = importedAssetPaths.Count;
        
        // 폴더 생성
        CreateFolderIfNotExists(selectedFolderPath);
        
        foreach (var assetPath in importedAssetPaths)
        {
            try
            {
                var originalName = Path.GetFileNameWithoutExtension(assetPath);
                var extension = Path.GetExtension(assetPath);
                var newName = GetPreviewName(originalName) + extension;
                var targetPath = Path.Combine(selectedFolderPath, newName).Replace("\\", "/");
                
                if (assetPath != targetPath)
                {
                    var error = AssetDatabase.MoveAsset(assetPath, targetPath);
                    
                    var result = new AssetProcessingResult(assetPath, targetPath, string.IsNullOrEmpty(error), error);
                    processingResults.Add(result);
                    
                    if (result.success)
                    {
                        Debug.Log($"✅ 파일 이동: {Path.GetFileName(assetPath)} → {selectedFolderPath}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 처리 실패: {assetPath} - {e.Message}");
            }
            
            processedCount++;
            processingProgress = (float)processedCount / totalCount;
        }
        
        FinishProcessing();
    }

    private void FinishProcessing()
    {
        AssetDatabase.Refresh();
        isProcessing = false;
        
        // 결과 통계
        var successCount = processingResults.Count(r => r.success);
        var failCount = processingResults.Count(r => !r.success);
        
        // 캐시 업데이트
        AssetOrganizerCache.UpdateProjectStats(processingResults.Count, successCount, successCount);
        
        // 결과 표시
        var message = $"처리 완료!\n✅ 성공: {successCount}개\n❌ 실패: {failCount}개";
        EditorUtility.DisplayDialog("처리 완료", message, "확인");
        
        Close();
    }

    private void CreateFolderIfNotExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        
        var pathParts = folderPath.Split('/');
        var currentPath = pathParts[0];
        
        for (int i = 1; i < pathParts.Length; i++)
        {
            var nextPath = currentPath + "/" + pathParts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, pathParts[i]);
            }
            currentPath = nextPath;
        }
    }

    private void CreateDefaultFolderSettings()
    {
        folderSettings = ScriptableObject.CreateInstance<FolderConventionSettings>();
        
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        AssetDatabase.CreateAsset(folderSettings, "Assets/Resources/FolderConventionSettings.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ FolderConventionSettings가 생성되었습니다.");
    }

    private void ShowSimpleTutorial()
    {
        var message = "📁 Asset Organizer 사용법\n\n" +
                     "1. 왼쪽에서 정리할 파일을 확인합니다\n" +
                     "2. 중앙에서 대상 폴더를 선택합니다\n" +
                     "3. 오른쪽에서 네이밍 규칙을 설정합니다\n" +
                     "4. [파일 정리하기] 버튼을 클릭합니다\n\n" +
                     "이 메시지를 다시 보지 않으시겠습니까?";
        
        var result = EditorUtility.DisplayDialogComplex("Asset Organizer", message, "확인", "다시 보지 않기", "취소");
        
        if (result == 1) // 다시 보지 않기
        {
            EditorPrefs.SetBool("AssetOrganizer_SkipSimpleTutorial", true);
        }
    }

    private void OnDestroy()
    {
        if (rootFolder != null)
        {
            rootFolder = null;
            folderNodeCache.Clear();
        }
    }
}