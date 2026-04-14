using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Holylib.DebugConsole {
    public static partial class DebugCommandRegistry {
        
        public static Dictionary<string, FieldInfo> NameToVariable = new Dictionary<string, FieldInfo>();
        private static void _registerVariables() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (Type type in assembly.GetTypes()) {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = field.GetCustomAttribute<DebugVariableAttribute>();
                            if (attribute != null) {
                                NameToVariable.TryAdd(field.Name,field);
                                
                                Commands.TryAdd(field.Name,new MethodGroup(null,NameToGroup[attribute.Group],field));
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
    
    
    [System.AttributeUsage(System.AttributeTargets.Field)]
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
