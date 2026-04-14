using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Holylib.DebugConsole {
    public static partial class DebugCommandRegistry {
        
        public static Dictionary<string, FieldInfo> NameToVariable = new Dictionary<string, FieldInfo>();
        public static Dictionary<string, PropertyInfo> NameToProperty = new Dictionary<string, PropertyInfo>();
        private static void _registerVariables() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (Type type in assembly.GetTypes()) {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = field.GetCustomAttribute<DebugVariableAttribute>();
                            if (attribute != null) {
                                NameToVariable.TryAdd(field.Name,field);
                                
                                Commands.TryAdd(field.Name,new MethodGroup(null,NameToGroup[attribute.Group],field,null));
                            }
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                    
                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = property.GetCustomAttribute<DebugVariableAttribute>();
                            if (attribute != null) {
                                NameToProperty.TryAdd(property.Name,property);
                                
                                Commands.TryAdd(property.Name,new MethodGroup(null,NameToGroup[attribute.Group],null,property));
                            }
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }
    }
    
    
    [System.AttributeUsage(System.AttributeTargets.Field| System.AttributeTargets.Property)]
    public class DebugVariableAttribute : System.Attribute {
        public string Group { get; }
        public DebugVariableAttribute (string group) {
            Group = group;
        }

        public DebugVariableAttribute() {
            Group = HolyDebugGroupStyles.Uncategorized;
        }
    }
}
