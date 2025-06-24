using UnityEngine;
using UnityEditor;

/// <summary>
/// Asset Organizer UI 스타일 헬퍼 (최적화 버전)
/// </summary>
public static class StyleHelper
{
    // ==================== 색상 정의 ====================
    public static class Colors
    {
        public static readonly Color Primary = new Color(0.2f, 0.6f, 1f, 1f);      // 파란색
        public static readonly Color Secondary = new Color(0.4f, 0.4f, 0.4f, 1f);  // 회색
        public static readonly Color Success = new Color(0.2f, 0.8f, 0.2f, 1f);    // 초록색
        public static readonly Color Warning = new Color(1f, 0.8f, 0.2f, 1f);      // 노란색
        public static readonly Color Error = new Color(1f, 0.3f, 0.3f, 1f);        // 빨간색
        public static readonly Color Info = new Color(0.5f, 0.8f, 1f, 1f);         // 하늘색
        
        // 텍스트 색상 - 가독성 개선
        public static readonly Color TextPrimary = new Color(0.1f, 0.1f, 0.1f, 1f);   // 진한 검정색
        public static readonly Color TextSecondary = new Color(0.3f, 0.3f, 0.3f, 1f);  // 중간 검정색  
        public static readonly Color TextMuted = new Color(0.5f, 0.5f, 0.5f, 1f);      // 연한 회색
        
        public static readonly Color Background = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static readonly Color BackgroundSecondary = new Color(0.25f, 0.25f, 0.25f, 1f);
        public static readonly Color Surface = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color Border = new Color(0.5f, 0.5f, 0.5f, 1f);
    }

    // ==================== 아이콘 정의 ====================
    public static class Icons
    {
        public const string Folder = "📁";
        public const string Settings = "⚙️";
        public const string Magic = "✨";
        public const string Error = "❌";
        public const string Warning = "⚠️";
        public const string Success = "✅";
        public const string Info = "ℹ️";
        public const string Delete = "🗑️";
        public const string Refresh = "🔄";
        public const string Help = "❓";
        public const string Badge = "🏷️";
    }

    // ==================== 캐시된 스타일들 ====================
    private static GUIStyle _headerStyle;
    private static GUIStyle _subHeaderStyle;
    private static GUIStyle _cardStyle;
    private static GUIStyle _buttonPrimaryStyle;
    private static GUIStyle _buttonSecondaryStyle;
    
    private static readonly System.Collections.Generic.Dictionary<Color, Texture2D> _textureCache = new ();

    public static GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleLeft,
                    margin = new RectOffset(0, 0, 5, 5)
                };
            }
            return _headerStyle;
        }
    }

    public static GUIStyle SubHeaderStyle
    {
        get
        {
            if (_subHeaderStyle == null)
            {
                _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = Colors.TextPrimary },
                    alignment = TextAnchor.MiddleLeft,
                    margin = new RectOffset(0, 0, 3, 3)
                };
            }
            return _subHeaderStyle;
        }
    }

    public static GUIStyle CardStyle
    {
        get
        {
            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 2, 2)
                };
            }
            return _cardStyle;
        }
    }

    public static GUIStyle ButtonPrimaryStyle
    {
        get
        {
            if (_buttonPrimaryStyle == null)
            {
                _buttonPrimaryStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { 
                        background = GetCachedTexture(Colors.Primary),
                        textColor = Color.white
                    },
                    hover = { 
                        background = GetCachedTexture(Colors.Primary * 1.1f),
                        textColor = Color.white
                    },
                    active = { 
                        background = GetCachedTexture(Colors.Primary * 0.9f),
                        textColor = Color.white
                    },
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(20, 20, 8, 8)
                };
            }
            return _buttonPrimaryStyle;
        }
    }

    public static GUIStyle ButtonSecondaryStyle
    {
        get
        {
            if (_buttonSecondaryStyle == null)
            {
                _buttonSecondaryStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { 
                        background = GetCachedTexture(Colors.Secondary),
                        textColor = Color.white
                    },
                    hover = { 
                        background = GetCachedTexture(Colors.Secondary * 1.1f),
                        textColor = Color.white
                    },
                    active = { 
                        background = GetCachedTexture(Colors.Secondary * 0.9f),
                        textColor = Color.white
                    },
                    padding = new RectOffset(15, 15, 6, 6)
                };
            }
            return _buttonSecondaryStyle;
        }
    }

    // ==================== 유틸리티 메서드들 ====================
    /// <summary>
    /// 캐시된 단색 텍스처 가져오기 (성능 최적화)
    /// </summary>
    private static Texture2D GetCachedTexture(Color color)
    {
        // 색상 키 생성 (소수점 2자리로 반올림하여 캐시 히트율 향상)
        var key = new Color(
            Mathf.Round(color.r * 100f) / 100f,
            Mathf.Round(color.g * 100f) / 100f,
            Mathf.Round(color.b * 100f) / 100f,
            Mathf.Round(color.a * 100f) / 100f
        );
        
        if (!_textureCache.TryGetValue(key, out Texture2D texture))
        {
            texture = CreateColorTexture(key);
            _textureCache[key] = texture;
        }
        
        return texture;
    }

    /// <summary>
    /// 단색 텍스처 생성
    /// </summary>
    private static Texture2D CreateColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// 진행률 바 그리기
    /// </summary>
    public static void DrawProgressBar(float progress, string label = "", float height = 20f)
    {
        var rect = GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true));
        
        // 배경
        EditorGUI.DrawRect(rect, Colors.BackgroundSecondary);
        
        // 진행률 바
        var progressRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
        EditorGUI.DrawRect(progressRect, Colors.Primary);
        
        // 텍스트
        if (!string.IsNullOrEmpty(label))
        {
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(rect, label, labelStyle);
        }
    }

    /// <summary>
    /// 아이콘 버튼 그리기
    /// </summary>
    public static bool DrawIconButton(string icon, string tooltip = "", float width = 30f, float height = 25f)
    {
        var iconStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        var content = new GUIContent(icon, tooltip);
        return GUILayout.Button(content, iconStyle, GUILayout.Width(width), GUILayout.Height(height));
    }

    /// <summary>
    /// 색상 있는 라벨 그리기
    /// </summary>
    public static void DrawColoredLabel(string text, Color color, int fontSize = 12, FontStyle fontStyle = FontStyle.Normal)
    {
        var style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = color },
            fontSize = fontSize,
            fontStyle = fontStyle
        };
        GUILayout.Label(text, style);
    }

    /// <summary>
    /// 통계 카드 그리기
    /// </summary>
    public static void DrawStatCard(string title, string value, Color color, float width = 120f, float height = 80f)
    {
        EditorGUILayout.BeginVertical(CardStyle, GUILayout.Width(width), GUILayout.Height(height));
        
        // 값
        var valueStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color }
        };
        GUILayout.Label(value, valueStyle);
        
        // 제목
        var titleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Colors.TextSecondary }
        };
        GUILayout.Label(title, titleStyle);
        
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 알림 메시지 그리기
    /// </summary>
    public static void DrawNotification(string message, Color backgroundColor, string icon = "")
    {
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor * 0.3f;
        
        EditorGUILayout.BeginHorizontal(CardStyle);
        
        if (!string.IsNullOrEmpty(icon))
        {
            var iconStyle = new GUIStyle { fontSize = 16 };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(25));
        }
        
        var messageStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Colors.TextPrimary },
            wordWrap = true
        };
        GUILayout.Label(message, messageStyle);
        
        EditorGUILayout.EndHorizontal();
        
        GUI.backgroundColor = originalColor;
    }

    /// <summary>
    /// 태그 그리기
    /// </summary>
    public static void DrawTag(string text, Color backgroundColor, Color textColor)
    {
        var tagStyle = new GUIStyle(EditorStyles.miniButton)
        {
            normal = { 
                background = GetCachedTexture(backgroundColor),
                textColor = textColor
            },
            fontSize = 10,
            padding = new RectOffset(8, 8, 2, 2),
            margin = new RectOffset(2, 2, 2, 2)
        };
        
        GUILayout.Label(text, tagStyle);
    }

    /// <summary>
    /// 배지 그리기
    /// </summary>
    public static void DrawBadge(string text, Color backgroundColor, float width = 60f)
    {
        var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { 
                background = GetCachedTexture(backgroundColor),
                textColor = Color.white
            },
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(6, 6, 4, 4),
            margin = new RectOffset(2, 2, 2, 2)
        };
        
        GUILayout.Label(text, badgeStyle, GUILayout.Width(width));
    }
    
    /// <summary>
    /// 캐시 정리 (메모리 관리)
    /// </summary>
    public static void ClearCache()
    {
        foreach (var texture in _textureCache.Values)
        {
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }
        }
        _textureCache.Clear();
        
        // 스타일 캐시도 정리
        _headerStyle = null;
        _subHeaderStyle = null;
        _cardStyle = null;
        _buttonPrimaryStyle = null;
        _buttonSecondaryStyle = null;
    }
}