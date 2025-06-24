#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 폴더 컨벤션 관리 에디터 (직관적 UI)
/// </summary>
public sealed class FolderConventionEditorWindow : EditorWindow
{
    // UI 상태
    [SerializeField] private FolderConventionSettings settings;
    private Vector2 listScrollPosition;
    private Vector2 detailScrollPosition;
    private int selectedIndex = -1;
    private string searchFilter = "";
    private bool showOnlyTopLevel = true;
    
    // 폴더 트리
    private class FolderView
    {
        public FolderConvention convention;
        public int depth;
        public bool isExpanded;
        public bool hasChildren;
    }
    
    private List<FolderView> visibleFolders = new List<FolderView>();
    private HashSet<string> expandedFolders = new HashSet<string>();
    
    // UI 캐시
    private bool needsRefresh = false;
    private double nextRepaintTime = 0;
    
    [MenuItem("Tools/Asset Organizer/폴더 컨벤션 관리 📁", priority = 20)]
    private static void ShowWindow()
    {
        var window = GetWindow<FolderConventionEditorWindow>();
        window.titleContent = new GUIContent("📁 폴더 컨벤션 관리");
        window.minSize = new Vector2(800, 500);
        window.Show();
    }
    
    private void OnEnable()
    {
        LoadSettings();
        RefreshFolderList();
    }
    
    private void LoadSettings()
    {
        settings = Resources.Load<FolderConventionSettings>("FolderConventionSettings");
        if (settings == null)
        {
            settings = CreateDefaultSettings();
        }
    }
    
    private FolderConventionSettings CreateDefaultSettings()
    {
        var newSettings = ScriptableObject.CreateInstance<FolderConventionSettings>();
        
        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        
        AssetDatabase.CreateAsset(newSettings, "Assets/Resources/FolderConventionSettings.asset");
        AssetDatabase.SaveAssets();
        
        return newSettings;
    }
    
    private void OnGUI()
    {
        DrawToolbar();
        
        EditorGUILayout.BeginHorizontal();
        DrawFolderListPanel();
        DrawDetailPanel();
        EditorGUILayout.EndHorizontal();
        
        // 주기적 다시 그리기
        if (EditorApplication.timeSinceStartup > nextRepaintTime)
        {
            Repaint();
            nextRepaintTime = EditorApplication.timeSinceStartup + 0.5;
        }
        
        // 새로고침 필요시
        if (needsRefresh)
        {
            RefreshFolderList();
            needsRefresh = false;
        }
    }
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // 새로고침 버튼
        if (GUILayout.Button(new GUIContent("🔄 새로고침", "폴더 목록 새로고침"), EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            RefreshFolderList();
        }
        
        // 최상위 폴더 스캔
        if (GUILayout.Button(new GUIContent("📂 최상위 스캔", "Assets 아래 최상위 폴더만 스캔"), EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            ScanTopLevelFolders();
        }
        
        GUILayout.Space(10);
        
        // 검색창
        EditorGUILayout.LabelField("🔍", GUILayout.Width(20));
        var newSearch = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        if (newSearch != searchFilter)
        {
            searchFilter = newSearch;
            RefreshFolderList();
        }
        
        if (GUILayout.Button("✖", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchFilter = "";
            RefreshFolderList();
            GUI.FocusControl(null);
        }
        
        GUILayout.Space(10);
        
        // 필터 토글
        showOnlyTopLevel = GUILayout.Toggle(showOnlyTopLevel, "최상위만", EditorStyles.toolbarButton, GUILayout.Width(60));
        
        GUILayout.FlexibleSpace();
        
        // 통계
        var statsStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };
        GUILayout.Label($"총 {settings.FolderConventions.Count}개 폴더", statsStyle);
        
        GUILayout.Space(10);
        
        // 저장 버튼
        GUI.backgroundColor = StyleHelper.Colors.Success;
        if (GUILayout.Button(new GUIContent("💾 저장", "변경사항 저장"), EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SaveSettings();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawFolderListPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(400));
        
        // 리스트 헤더
        var headerRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
        
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUI.LabelField(headerRect, "📁 폴더 목록", headerStyle);
        
        // 폴더 리스트
        listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition);
        
        for (int i = 0; i < visibleFolders.Count; i++)
        {
            DrawFolderItem(visibleFolders[i], i);
        }
        
        EditorGUILayout.EndScrollView();
        
        // 하단 버튼
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("➕ 폴더 추가", GUILayout.Height(25)))
        {
            ShowAddFolderMenu();
        }
        
        GUI.enabled = selectedIndex >= 0;
        if (GUILayout.Button("➖ 폴더 제거", GUILayout.Height(25)))
        {
            RemoveSelectedFolder();
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawFolderItem(FolderView folderView, int index)
    {
        var folder = folderView.convention;
        var isSelected = selectedIndex == index;
        
        // 아이템 배경
        var itemRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(28));
        
        if (isSelected)
        {
            EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.6f, 1f, 0.3f));
        }
        else if (itemRect.Contains(Event.current.mousePosition))
        {
            EditorGUI.DrawRect(itemRect, new Color(1f, 1f, 1f, 0.1f));
        }
        
        // 들여쓰기
        GUILayout.Space(folderView.depth * 20);
        
        // 확장/축소 버튼
        if (folderView.hasChildren)
        {
            var expanded = expandedFolders.Contains(folder.folderPath);
            var foldoutContent = new GUIContent(expanded ? "▼" : "▶");
            
            if (GUILayout.Button(foldoutContent, EditorStyles.label, GUILayout.Width(15)))
            {
                if (expanded)
                    expandedFolders.Remove(folder.folderPath);
                else
                    expandedFolders.Add(folder.folderPath);
                
                needsRefresh = true;
            }
        }
        else
        {
            GUILayout.Space(17);
        }
        
        // 폴더 아이콘
        var iconStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 16,
            normal = { textColor = folder.folderColor }
        };
        GUILayout.Label(folder.folderIcon, iconStyle, GUILayout.Width(20));
        
        // 폴더명
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = folderView.depth == 1 ? FontStyle.Bold : FontStyle.Normal
        };
        
        if (GUILayout.Button(folder.displayName, nameStyle, GUILayout.Height(24)))
        {
            selectedIndex = index;
            GUI.FocusControl(null);
        }
        
        GUILayout.FlexibleSpace();
        
        // 상태 아이콘들
        if (folder.enforceNaming)
        {
            GUILayout.Label(new GUIContent("✓", "네이밍 규칙 적용"), GUILayout.Width(20));
        }
        
        if (folder.autoApply)
        {
            GUILayout.Label(new GUIContent("⚡", "자동 적용"), GUILayout.Width(20));
        }
        
        // 폴더 열기 버튼
        if (GUILayout.Button(new GUIContent("📂", "폴더 열기"), EditorStyles.label, GUILayout.Width(20)))
        {
            OpenFolder(folder.folderPath);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawDetailPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        // 상세 정보 헤더
        var headerRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
        
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        
        if (selectedIndex < 0 || selectedIndex >= visibleFolders.Count)
        {
            EditorGUI.LabelField(headerRect, "📋 폴더를 선택하세요", headerStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }
        
        var selectedFolder = visibleFolders[selectedIndex].convention;
        EditorGUI.LabelField(headerRect, $"📋 {selectedFolder.displayName} 설정", headerStyle);
        
        // 상세 정보 스크롤
        detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);
        
        // SerializedObject로 편집
        var so = new SerializedObject(settings);
        var conventionsArray = so.FindProperty("folderConventions");
        
        // 실제 인덱스 찾기
        int realIndex = -1;
        for (int i = 0; i < settings.FolderConventions.Count; i++)
        {
            if (settings.FolderConventions[i] == selectedFolder)
            {
                realIndex = i;
                break;
            }
        }
        
        if (realIndex >= 0)
        {
            var element = conventionsArray.GetArrayElementAtIndex(realIndex);
            
            EditorGUI.BeginChangeCheck();
            
            // 기본 정보
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📁 기본 정보", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(element.FindPropertyRelative("displayName"), new GUIContent("표시 이름"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("description"), new GUIContent("설명"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("folderIcon"), new GUIContent("아이콘"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("folderColor"), new GUIContent("색상"));
            
            // 네이밍 규칙
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📝 네이밍 규칙", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(element.FindPropertyRelative("enforceNaming"), new GUIContent("규칙 적용"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("prefix"), new GUIContent("접두사"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("suffix"), new GUIContent("접미사"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("namingStyle"), new GUIContent("네이밍 스타일"));
            
            // 자동 처리
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚡ 자동 처리", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(element.FindPropertyRelative("autoApply"), new GUIContent("자동 적용"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("autoCapitalize"), new GUIContent("자동 대문자화"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("removeSpecialChars"), new GUIContent("특수문자 제거"));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("preserveNumbers"), new GUIContent("숫자 보존"));
            
            // 파일 타입
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("📄 파일 타입", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(element.FindPropertyRelative("allowedExtensions"), new GUIContent("허용 확장자"), true);
            
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                needsRefresh = true;
            }
        }
        
        EditorGUILayout.EndScrollView();
        
        // 하단 액션 버튼
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🔍 하위 폴더 스캔", GUILayout.Height(30)))
        {
            ScanSubfolders(selectedFolder.folderPath);
        }
        
        if (GUILayout.Button("📊 컴플라이언스 체크", GUILayout.Height(30)))
        {
            CheckFolderCompliance(selectedFolder);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void RefreshFolderList()
    {
        visibleFolders.Clear();
        
        if (settings == null || settings.FolderConventions == null) return;
        
        var conventions = settings.FolderConventions.ToList();
        conventions.Sort((a, b) => string.Compare(a.folderPath, b.folderPath, StringComparison.Ordinal));
        
        foreach (var convention in conventions)
        {
            // 필터링
            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (!convention.displayName.ToLower().Contains(searchFilter.ToLower()) &&
                    !convention.folderPath.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }
            }
            
            var depth = convention.folderPath.Count(c => c == '/') - 1;
            
            if (showOnlyTopLevel && depth > 1) continue;
            
            // 부모 폴더가 축소되어 있는지 확인
            var parentPath = Path.GetDirectoryName(convention.folderPath)?.Replace("\\", "/");
            bool isVisible = true;
            
            while (!string.IsNullOrEmpty(parentPath) && parentPath != "Assets")
            {
                if (!expandedFolders.Contains(parentPath))
                {
                    isVisible = false;
                    break;
                }
                parentPath = Path.GetDirectoryName(parentPath)?.Replace("\\", "/");
            }
            
            if (!isVisible && string.IsNullOrEmpty(searchFilter)) continue;
            
            // 하위 폴더 존재 여부 확인
            bool hasChildren = conventions.Any(c => 
                c != convention && 
                c.folderPath.StartsWith(convention.folderPath + "/"));
            
            visibleFolders.Add(new FolderView
            {
                convention = convention,
                depth = depth,
                isExpanded = expandedFolders.Contains(convention.folderPath),
                hasChildren = hasChildren
            });
        }
    }
    
    private void ScanTopLevelFolders()
    {
        if (EditorUtility.DisplayDialog("최상위 폴더 스캔", 
            "Assets 폴더 아래의 최상위 폴더만 스캔합니다.\n기존 하위 폴더 설정은 삭제됩니다.", 
            "스캔", "취소"))
        {
            settings.ScanTopLevelFoldersOnly();
            RefreshFolderList();
            selectedIndex = -1;
            expandedFolders.Clear();
            SaveSettings();
        }
    }
    
    private void ScanSubfolders(string parentPath)
    {
        settings.ScanSubfoldersOf(parentPath);
        expandedFolders.Add(parentPath);
        RefreshFolderList();
        SaveSettings();
    }
    
    private void ShowAddFolderMenu()
    {
        var menu = new GenericMenu();
        
        // 기존 폴더에서 선택
        menu.AddItem(new GUIContent("기존 폴더 선택..."), false, () =>
        {
            var folderPath = EditorUtility.OpenFolderPanel("폴더 선택", "Assets", "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                // 상대 경로로 변환
                if (folderPath.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
                    AddNewFolder(folderPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("오류", "Assets 폴더 내의 폴더를 선택하세요.", "확인");
                }
            }
        });
        
        menu.AddSeparator("");
        
        // 프리셋
        menu.AddItem(new GUIContent("프리셋/텍스처 폴더"), false, () => AddPresetFolder("Textures", "T_"));
        menu.AddItem(new GUIContent("프리셋/UI 폴더"), false, () => AddPresetFolder("UI", "UI_"));
        menu.AddItem(new GUIContent("프리셋/오디오 폴더"), false, () => AddPresetFolder("Audio", "Audio_"));
        menu.AddItem(new GUIContent("프리셋/모델 폴더"), false, () => AddPresetFolder("Models", "M_"));
        menu.AddItem(new GUIContent("프리셋/머티리얼 폴더"), false, () => AddPresetFolder("Materials", "Mat_"));
        
        menu.ShowAsContext();
    }
    
    private void AddNewFolder(string folderPath)
    {
        if (settings.GetConventionForFolder(folderPath) != null)
        {
            EditorUtility.DisplayDialog("알림", "이미 등록된 폴더입니다.", "확인");
            return;
        }
        
        settings.TryRegister(folderPath);
        RefreshFolderList();
        SaveSettings();
    }
    
    private void AddPresetFolder(string folderName, string prefix)
    {
        var folderPath = $"Assets/{folderName}";
        
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", folderName);
        }
        
        var convention = new FolderConvention
        {
            folderPath = folderPath,
            displayName = folderName,
            prefix = prefix,
            namingStyle = NamingStyle.PascalCase,
            enforceNaming = true
        };
        
        settings.AddFolderConvention(convention);
        RefreshFolderList();
        SaveSettings();
    }
    
    private void RemoveSelectedFolder()
    {
        if (selectedIndex < 0 || selectedIndex >= visibleFolders.Count) return;
        
        var folder = visibleFolders[selectedIndex].convention;
        
        if (EditorUtility.DisplayDialog("폴더 제거", 
            $"'{folder.displayName}' 폴더 설정을 제거하시겠습니까?", 
            "제거", "취소"))
        {
            settings.RemoveFolderConvention(folder.folderPath);
            RefreshFolderList();
            selectedIndex = -1;
            SaveSettings();
        }
    }
    
    private void CheckFolderCompliance(FolderConvention folder)
    {
        try
        {
            var guids = AssetDatabase.FindAssets("", new[] { folder.folderPath });
            int totalFiles = 0;
            int compliantFiles = 0;
            var violations = new List<string>();
            
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                
                // 직접 하위 파일만 체크
                if (Path.GetDirectoryName(assetPath).Replace("\\", "/") != folder.folderPath)
                    continue;
                
                totalFiles++;
                
                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                var extension = Path.GetExtension(assetPath);
                var issues = folder.ValidateFile(fileName, extension);
                
                if (issues.Count == 0)
                {
                    compliantFiles++;
                }
                else
                {
                    violations.Add($"{fileName}: {string.Join(", ", issues)}");
                }
            }
            
            float complianceRate = totalFiles > 0 ? (float)compliantFiles / totalFiles * 100 : 100;
            
            var message = $"📊 컴플라이언스 체크 결과\n\n" +
                         $"폴더: {folder.displayName}\n" +
                         $"총 파일: {totalFiles}개\n" +
                         $"준수: {compliantFiles}개\n" +
                         $"위반: {totalFiles - compliantFiles}개\n" +
                         $"준수율: {complianceRate:F1}%\n";
            
            if (violations.Count > 0)
            {
                message += "\n위반 사항:\n";
                foreach (var violation in violations.Take(10))
                {
                    message += $"• {violation}\n";
                }
                
                if (violations.Count > 10)
                {
                    message += $"... 외 {violations.Count - 10}개";
                }
            }
            
            EditorUtility.DisplayDialog("컴플라이언스 체크", message, "확인");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("오류", $"체크 중 오류 발생: {e.Message}", "확인");
        }
    }
    
    private void OpenFolder(string folderPath)
    {
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
        if (folder != null)
        {
            EditorGUIUtility.PingObject(folder);
            Selection.activeObject = folder;
        }
    }
    
    private void SaveSettings()
    {
        if (settings != null)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("✅ 저장 완료"));
        }
    }
}
#endif