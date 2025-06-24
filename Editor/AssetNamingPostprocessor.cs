#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetOrganizer
{
    /// <summary>
    /// 에셋 자동 처리 포스트프로세서 (안정화 버전)
    /// </summary>
    public sealed class AssetNamingPostprocessor : AssetPostprocessor
    {
        // 성능 설정
        private const int BATCH_SIZE = 50; // 한 번에 처리할 최대 파일 수
        private const int DIALOG_THRESHOLD = 10; // 다이얼로그 표시 임계값
        private static readonly TimeSpan PROCESS_COOLDOWN = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan DIALOG_COOLDOWN = TimeSpan.FromSeconds(5);

        // 상태 관리
        private static readonly HashSet<string> processedAssets = new HashSet<string>();
        private static readonly Queue<string> pendingAssets = new Queue<string>();
        private static bool isInitialized = false;
        private static DateTime lastProcessTime = DateTime.MinValue;
        private static DateTime lastDialogTime = DateTime.MinValue;
        private static bool isProcessing = false;

        // 설정 캐시
        private static FolderConventionSettings cachedSettings;
        private static DateTime settingsCacheTime;
        private static readonly TimeSpan CACHE_LIFETIME = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 에디터 시작 시 초기화
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (isInitialized) return;
            
            EditorApplication.delayCall += DelayedInitialize;
        }

        private static void DelayedInitialize()
        {
            try
            {
                LoadProcessedAssets();
                isInitialized = true;
                Debug.Log("[AssetOrganizer] ✅ 포스트프로세서 초기화 완료");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetOrganizer] ❌ 초기화 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 기존 에셋 로드 (성능 최적화)
        /// </summary>
        private static void LoadProcessedAssets()
        {
            processedAssets.Clear();
            
            // 최상위 폴더만 스캔하여 초기 로딩 시간 단축
            var topLevelFolders = AssetDatabase.GetSubFolders("Assets");
            
            foreach (var folder in topLevelFolders)
            {
                if (IsExcludedFolder(folder)) continue;
                
                // 각 폴더의 직접 하위 파일만 로드
                var guids = AssetDatabase.FindAssets("", new[] { folder });
                
                foreach (var guid in guids.Take(100)) // 폴더당 최대 100개만
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsValidAssetPath(path) && Path.GetDirectoryName(path) == folder)
                    {
                        processedAssets.Add(path);
                    }
                }
            }
        }

        /// <summary>
        /// 에셋 임포트 후 처리
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!GetAutoProcessEnabled() || isProcessing) return;
            
            try
            {
                isProcessing = true;
                
                // 삭제된 에셋 처리
                if (deletedAssets != null)
                {
                    foreach (var asset in deletedAssets)
                    {
                        processedAssets.Remove(asset);
                    }
                }
                
                // 이동된 에셋 처리
                if (movedAssets != null && movedFromAssetPaths != null)
                {
                    for (int i = 0; i < movedAssets.Length; i++)
                    {
                        processedAssets.Remove(movedFromAssetPaths[i]);
                        processedAssets.Add(movedAssets[i]);
                    }
                }
                
                // 새로 임포트된 에셋 처리
                if (importedAssets != null && importedAssets.Length > 0)
                {
                    ProcessImportedAssets(importedAssets);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetOrganizer] ❌ 에셋 처리 중 오류: {e.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        /// <summary>
        /// 임포트된 에셋 처리
        /// </summary>
        private static void ProcessImportedAssets(string[] importedAssets)
        {
            if (!isInitialized)
            {
                // 초기화되지 않았으면 대기열에 추가
                foreach (var asset in importedAssets)
                {
                    if (IsValidAssetPath(asset))
                    {
                        pendingAssets.Enqueue(asset);
                    }
                }
                return;
            }
            
            // 쿨다운 체크
            if (DateTime.Now - lastProcessTime < PROCESS_COOLDOWN)
            {
                foreach (var asset in importedAssets)
                {
                    if (IsValidAssetPath(asset))
                    {
                        pendingAssets.Enqueue(asset);
                    }
                }
                EditorApplication.delayCall += ProcessPendingAssets;
                return;
            }
            
            lastProcessTime = DateTime.Now;
            
            // 새 에셋 필터링
            var newAssets = FilterNewAssets(importedAssets);
            if (newAssets.Count == 0) return;
            
            // 캐시에 추가
            AssetOrganizerCache.AddPaths(newAssets);
            
            // 배치 처리
            for (int i = 0; i < newAssets.Count; i += BATCH_SIZE)
            {
                var batch = newAssets.Skip(i).Take(BATCH_SIZE).ToList();
                ProcessBatch(batch);
            }
        }

        /// <summary>
        /// 대기 중인 에셋 처리
        /// </summary>
        private static void ProcessPendingAssets()
        {
            if (pendingAssets.Count == 0 || isProcessing) return;
            
            var assets = new List<string>();
            while (pendingAssets.Count > 0 && assets.Count < BATCH_SIZE)
            {
                assets.Add(pendingAssets.Dequeue());
            }
            
            ProcessImportedAssets(assets.ToArray());
        }

        /// <summary>
        /// 새 에셋 필터링
        /// </summary>
        private static List<string> FilterNewAssets(IEnumerable<string> assets)
        {
            var settings = GetSettings();
            var newAssets = new List<string>();
            
            foreach (var asset in assets)
            {
                if (!IsValidAssetPath(asset)) continue;
                if (processedAssets.Contains(asset)) continue;
                if (settings != null && settings.IsGloballyExcluded(asset)) continue;
                
                newAssets.Add(asset);
                processedAssets.Add(asset);
            }
            
            return newAssets;
        }

        /// <summary>
        /// 배치 처리
        /// </summary>
        private static void ProcessBatch(List<string> assets)
        {
            var settings = GetSettings();
            if (settings == null)
            {
                settings = CreateDefaultSettings();
            }
            
            // 폴더 등록
            var folders = new HashSet<string>();
            foreach (var asset in assets)
            {
                var folder = Path.GetDirectoryName(asset)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(folder))
                {
                    folders.Add(folder);
                }
            }
            
            foreach (var folder in folders)
            {
                settings.TryRegister(folder);
            }
            
            // 다이얼로그 표시 여부 결정
            bool showDialog = GetShowDialogEnabled() && 
                             settings.ShowFolderSelectionDialog &&
                             assets.Count <= DIALOG_THRESHOLD &&
                             DateTime.Now - lastDialogTime > DIALOG_COOLDOWN;
            
            if (showDialog)
            {
                lastDialogTime = DateTime.Now;
                // 다이얼로그 표시는 지연 실행
                EditorApplication.delayCall += () => 
                {
                    try
                    {
                        AssetImportDialog.ShowForAssets(assets);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AssetOrganizer] ❌ 다이얼로그 표시 실패: {e.Message}");
                    }
                };
            }
            else
            {
                // 자동 처리
                AutoProcessAssets(assets, settings);
            }
        }

        /// <summary>
        /// 에셋 자동 처리
        /// </summary>
        private static void AutoProcessAssets(List<string> assets, FolderConventionSettings settings)
        {
            int processedCount = 0;
            int renamedCount = 0;
            
            foreach (var asset in assets)
            {
                try
                {
                    var convention = settings.FindConventionForPath(asset);
                    if (convention != null && convention.autoApply)
                    {
                        if (ApplyConvention(asset, convention))
                        {
                            renamedCount++;
                        }
                    }
                    processedCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AssetOrganizer] ⚠️ 에셋 처리 실패 '{asset}': {e.Message}");
                }
            }
            
            if (renamedCount > 0)
            {
                AssetDatabase.Refresh();
                AssetOrganizerCache.UpdateProjectStats(processedCount, renamedCount, 0);
                Debug.Log($"[AssetOrganizer] ✅ {renamedCount}개 파일 자동 정리 완료");
            }
        }

        /// <summary>
        /// 컨벤션 적용
        /// </summary>
        private static bool ApplyConvention(string assetPath, FolderConvention convention)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                var extension = Path.GetExtension(assetPath);
                var directory = Path.GetDirectoryName(assetPath);
                
                var newName = convention.ApplyConvention(fileName, extension);
                if (fileName == newName) return false;
                
                var newPath = Path.Combine(directory, newName + extension).Replace("\\", "/");
                
                // 중복 체크
                if (File.Exists(newPath))
                {
                    Debug.LogWarning($"[AssetOrganizer] ⚠️ 파일명 충돌: {newPath}");
                    return false;
                }
                
                var error = AssetDatabase.MoveAsset(assetPath, newPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning($"[AssetOrganizer] ⚠️ 이름 변경 실패: {error}");
                    return false;
                }
                
                // 캐시 업데이트
                processedAssets.Remove(assetPath);
                processedAssets.Add(newPath);
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetOrganizer] ❌ 컨벤션 적용 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 유효한 에셋 경로인지 확인
        /// </summary>
        private static bool IsValidAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("Assets/")) return false;
            if (path.EndsWith(".meta")) return false;
            if (path.Contains("/__")) return false; // 숨김 폴더
            if (path.Contains("/.")) return false; // 숨김 파일
            
            return true;
        }

        /// <summary>
        /// 제외 폴더인지 확인
        /// </summary>
        private static bool IsExcludedFolder(string folder)
        {
            var excludedFolders = new[] { "Packages", "Library", "Temp", "Build", "UserSettings" };
            return excludedFolders.Any(excluded => folder.Contains(excluded));
        }

        /// <summary>
        /// 설정 가져오기 (캐시)
        /// </summary>
        private static FolderConventionSettings GetSettings()
        {
            if (cachedSettings == null || DateTime.Now - settingsCacheTime > CACHE_LIFETIME)
            {
                cachedSettings = Resources.Load<FolderConventionSettings>("FolderConventionSettings");
                settingsCacheTime = DateTime.Now;
            }
            return cachedSettings;
        }

        /// <summary>
        /// 기본 설정 생성
        /// </summary>
        private static FolderConventionSettings CreateDefaultSettings()
        {
            var settings = ScriptableObject.CreateInstance<FolderConventionSettings>();
            
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }
            
            AssetDatabase.CreateAsset(settings, "Assets/Resources/FolderConventionSettings.asset");
            AssetDatabase.SaveAssets();
            
            cachedSettings = settings;
            settingsCacheTime = DateTime.Now;
            
            Debug.Log("[AssetOrganizer] ✅ 기본 설정 생성 완료");
            
            return settings;
        }

        // ==================== 메뉴 아이템 ====================
        
        [MenuItem("Tools/Asset Organizer/자동 처리 활성화", priority = 10)]
        private static void ToggleAutoProcess()
        {
            SetAutoProcessEnabled(!GetAutoProcessEnabled());
        }
        
        [MenuItem("Tools/Asset Organizer/자동 처리 활성화", validate = true)]
        private static bool ToggleAutoProcessValidate()
        {
            Menu.SetChecked("Tools/Asset Organizer/자동 처리 활성화", GetAutoProcessEnabled());
            return true;
        }
        
        [MenuItem("Tools/Asset Organizer/다이얼로그 표시", priority = 11)]
        private static void ToggleShowDialog()
        {
            SetShowDialogEnabled(!GetShowDialogEnabled());
        }
        
        [MenuItem("Tools/Asset Organizer/다이얼로그 표시", validate = true)]
        private static bool ToggleShowDialogValidate()
        {
            Menu.SetChecked("Tools/Asset Organizer/다이얼로그 표시", GetShowDialogEnabled());
            return true;
        }
        
        [MenuItem("Tools/Asset Organizer/캐시 초기화", priority = 30)]
        private static void ClearCache()
        {
            processedAssets.Clear();
            pendingAssets.Clear();
            AssetOrganizerCache.Clear();
            cachedSettings = null;
            isInitialized = false;
            Initialize();
            Debug.Log("[AssetOrganizer] ✅ 캐시 초기화 완료");
        }
        
        [MenuItem("Tools/Asset Organizer/통계 보기", priority = 31)]
        private static void ShowStats()
        {
            var stats = AssetOrganizerCache.GetDetailedStats();
            var message = $"📊 Asset Organizer 통계\n\n" +
                         $"처리된 에셋: {processedAssets.Count}\n" +
                         $"대기 중: {pendingAssets.Count}\n" +
                         $"총 처리: {stats["TotalProcessed"]}\n" +
                         $"이름 변경: {stats["TotalRenamed"]}\n" +
                         $"폴더 이동: {stats["TotalMoved"]}";
            
            EditorUtility.DisplayDialog("Asset Organizer 통계", message, "확인");
        }
        
        // ==================== 설정 접근자 ====================
        
        private static bool GetAutoProcessEnabled()
        {
            return EditorPrefs.GetBool("AssetOrganizer_AutoProcess", true);
        }
        
        private static void SetAutoProcessEnabled(bool value)
        {
            EditorPrefs.SetBool("AssetOrganizer_AutoProcess", value);
            Debug.Log($"[AssetOrganizer] 자동 처리: {(value ? "활성화" : "비활성화")}");
        }
        
        private static bool GetShowDialogEnabled()
        {
            return EditorPrefs.GetBool("AssetOrganizer_ShowDialog", true);
        }
        
        private static void SetShowDialogEnabled(bool value)
        {
            EditorPrefs.SetBool("AssetOrganizer_ShowDialog", value);
            Debug.Log($"[AssetOrganizer] 다이얼로그 표시: {(value ? "활성화" : "비활성화")}");
        }
    }
}
#endif