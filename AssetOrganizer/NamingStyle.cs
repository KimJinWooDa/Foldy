using UnityEngine;

public enum NamingStyle 
{
    [InspectorName("그대로 유지")]
    AsIs,
    
    [InspectorName("파스칼 케이스 (PascalCase)")]
    PascalCase,
    
    [InspectorName("카멜 케이스 (camelCase)")]
    CamelCase,
    
    [InspectorName("스네이크 케이스 (snake_case)")]
    SnakeCase,
    
    [InspectorName("케밥 케이스 (kebab-case)")]
    KebabCase,
    
    [InspectorName("모두 대문자 (UPPERCASE)")]
    UpperCase,
    
    [InspectorName("모두 소문자 (lowercase)")]
    LowerCase
}