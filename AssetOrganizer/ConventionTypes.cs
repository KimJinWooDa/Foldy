using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 컨벤션 규칙 정의 클래스
/// </summary>
[Serializable]
public class ConventionRule
{
    [Header("규칙 정보")]
    public string name = "";
    public string pattern = "";
    
    [TextArea(2, 4)]
    public string description = "";
    
    [Header("설정")]
    public bool isRequired = true;
    
    [Header("예시")]
    [TextArea(1, 3)]
    public string goodExample = "";
    
    [TextArea(1, 3)]
    public string badExample = "";

    /// <summary>
    /// 텍스트가 이 규칙에 맞는지 확인
    /// </summary>
    public bool IsMatch(string text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return false;
            
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, pattern);
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 기본 생성자
    /// </summary>
    public ConventionRule()
    {
    }

    /// <summary>
    /// 빠른 생성을 위한 생성자
    /// </summary>
    public ConventionRule(string name, string pattern, string description, bool isRequired = true)
    {
        this.name = name;
        this.pattern = pattern;
        this.description = description;
        this.isRequired = isRequired;
    }
}

/// <summary>
/// 에셋 처리 결과
/// </summary>
[Serializable]
public class AssetProcessingResult
{
    public string originalPath = "";
    public string newPath = "";
    public string newName = "";
    public bool success = false;
    public string errorMessage = "";
    public DateTime processedTime = DateTime.Now;
    
    public AssetProcessingResult()
    {
    }
    
    public AssetProcessingResult(string original, string newPath, bool success, string error = "")
    {
        this.originalPath = original;
        this.newPath = newPath;
        this.newName = System.IO.Path.GetFileName(newPath);
        this.success = success;
        this.errorMessage = error;
    }
}

/// <summary>
/// 폴더 컴플라이언스 정보
/// </summary>
[Serializable]
public class FolderComplianceInfo
{
    public string folderPath = "";
    public int totalAssets = 0;
    public int compliantAssets = 0;
    public float complianceRate => totalAssets > 0 ? (float)compliantAssets / totalAssets : 1f;
    public DateTime lastChecked = DateTime.Now;
    
    public string GetComplianceGrade()
    {
        float rate = complianceRate * 100;
        return rate switch
        {
            >= 95f => "S",
            >= 90f => "A",
            >= 80f => "B",
            >= 70f => "C",
            >= 60f => "D",
            _ => "F"
        };
    }
    
    public Color GetGradeColor()
    {
        return GetComplianceGrade() switch
        {
            "S" => new Color(1f, 0.8f, 0f, 1f), // Gold
            "A" => Color.green,
            "B" => Color.cyan,
            "C" => Color.yellow,
            "D" => new Color(1f, 0.5f, 0f, 1f), // Orange
            "F" => Color.red,
            _ => Color.gray
        };
    }
}