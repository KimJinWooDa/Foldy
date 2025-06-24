using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

/// <summary>
/// 에셋 오거나이저 캐시 시스템 (스크립트 관련 기능 제거됨)
/// </summary>
public static class AssetOrganizerCache 
{
    // ==================== 이벤트 시스템 ====================
    public static event Action<List<string>> OnPendingAssetsChanged;
    public static event Action<ProjectStats> OnStatsUpdated;
    public static event Action OnCacheCleared;

    // ==================== 캐시 데이터 ====================
    private static readonly List<string> pendingAssetPaths = new List<string>();
    private static readonly Dictionary<string, DateTime> lastCheckTimes = new Dictionary<string, DateTime>();
    
    // 프로젝트 통계
    private static ProjectStats projectStats = new ProjectStats();
    
    // 성능 관련
    private static readonly object lockObject = new object();

    // ==================== Properties ====================
    public static IReadOnlyList<string> PendingAssetPaths 
    { 
        get 
        { 
            lock (lockObject) 
            { 
                return pendingAssetPaths.ToList(); 
            } 
        } 
    }
    
    public static ProjectStats ProjectStats 
    { 
        get 
        { 
            lock (lockObject) 
            { 
                return projectStats; 
            } 
        } 
    }

    // ==================== Pending Assets 관리 ====================
    /// <summary>
    /// 대기 중인 에셋 경로 추가
    /// </summary>
    public static void AddPaths(IEnumerable<string> paths) 
    {
        if (paths == null) return;
        
        lock (lockObject)
        {
            bool hasChanges = false;
            
            foreach (var path in paths) 
            {
                if (!string.IsNullOrEmpty(path) && !pendingAssetPaths.Contains(path))
                {
                    pendingAssetPaths.Add(path);
                    hasChanges = true;
                }
            }
            
            if (hasChanges)
            {
                OnPendingAssetsChanged?.Invoke(pendingAssetPaths.ToList());
                Debug.Log($"[AssetOrganizer] 📥 Added {paths.Count()} new assets to queue. Total: {pendingAssetPaths.Count}");
            }
        }
    }

    /// <summary>
    /// 대기 중인 에셋 경로 제거
    /// </summary>
    public static void RemovePaths(IEnumerable<string> paths) 
    {
        if (paths == null) return;
        
        lock (lockObject)
        {
            int removedCount = 0;
            
            foreach (var path in paths)
            {
                if (pendingAssetPaths.Remove(path))
                {
                    removedCount++;
                }
            }
            
            if (removedCount > 0)
            {
                OnPendingAssetsChanged?.Invoke(pendingAssetPaths.ToList());
                Debug.Log($"[AssetOrganizer] ✅ Processed {removedCount} assets. Remaining: {pendingAssetPaths.Count}");
            }
        }
    }

    /// <summary>
    /// 특정 에셋 경로 존재 여부 확인
    /// </summary>
    public static bool ContainsPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        
        lock (lockObject)
        {
            return pendingAssetPaths.Contains(path);
        }
    }

    // ==================== 통계 관리 ====================
    /// <summary>
    /// 프로젝트 통계 업데이트
    /// </summary>
    public static void UpdateProjectStats(int processedAssets, int renamedAssets, int movedAssets)
    {
        lock (lockObject)
        {
            projectStats.TotalProcessedAssets += processedAssets;
            projectStats.TotalRenamedAssets += renamedAssets;
            projectStats.TotalMovedAssets += movedAssets;
            projectStats.LastUpdateTime = DateTime.Now;
            
            OnStatsUpdated?.Invoke(projectStats);
            
            Debug.Log($"[AssetOrganizer] 📊 Updated stats: {processedAssets} processed, {renamedAssets} renamed, {movedAssets} moved");
        }
    }

    /// <summary>
    /// 통계 가져오기
    /// </summary>
    public static Dictionary<string, object> GetDetailedStats()
    {
        lock (lockObject)
        {
            return new Dictionary<string, object>
            {
                ["PendingAssets"] = pendingAssetPaths.Count,
                ["TotalProcessed"] = projectStats.TotalProcessedAssets,
                ["TotalRenamed"] = projectStats.TotalRenamedAssets,
                ["TotalMoved"] = projectStats.TotalMovedAssets,
                ["LastUpdate"] = projectStats.LastUpdateTime
            };
        }
    }

    // ==================== 캐시 정리 ====================
    /// <summary>
    /// 전체 캐시 클리어
    /// </summary>
    public static void Clear() 
    {
        lock (lockObject)
        {
            pendingAssetPaths.Clear();
            lastCheckTimes.Clear();
            projectStats = new ProjectStats();
            
            OnCacheCleared?.Invoke();
            
            Debug.Log("[AssetOrganizer] 🧹 Cleared all cache data");
        }
    }

    /// <summary>
    /// 존재하지 않는 파일들 정리
    /// </summary>
    public static void CleanupMissingFiles()
    {
        lock (lockObject)
        {
            var missingFiles = new List<string>();
            
            // 대기 중인 에셋 중 존재하지 않는 파일 찾기
            foreach (var path in pendingAssetPaths.ToList())
            {
                if (!System.IO.File.Exists(path))
                {
                    missingFiles.Add(path);
                    pendingAssetPaths.Remove(path);
                }
            }
            
            if (missingFiles.Count > 0)
            {
                Debug.Log($"[AssetOrganizer] 🧹 Cleaned up {missingFiles.Count} missing files from cache");
            }
        }
    }
}

/// <summary>
/// 프로젝트 통계 정보
/// </summary>
[Serializable]
public class ProjectStats
{
    public int TotalProcessedAssets = 0;
    public int TotalRenamedAssets = 0;
    public int TotalMovedAssets = 0;
    public DateTime LastUpdateTime = DateTime.Now;
}