using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Holylib.DebugConsole {
    public static partial class DebugCommandRegistry {

        public static Dictionary<string, FieldInfo> NameToVariable = new Dictionary<string, FieldInfo>();
        public static Dictionary<string, PropertyInfo> NameToProperty = new Dictionary<string, PropertyInfo>();
        private static void _registerVariables (Type type) {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                try {
                    var attribute = field.GetCustomAttribute<DebugVariableAttribute>();
                    if (attribute != null) {
                        NameToVariable.TryAdd(field.Name, field);

                        DebugGroupStyle style;
                        if (NameToGroup.TryGetValue(attribute.Group, out DebugGroupStyle group)) {
                            style = group;
                        } else {
                            NameToGroup[attribute.Group] = new DebugGroupStyle(attribute.Group, Color.white);
                            style = NameToGroup[attribute.Group];
                        }
                        
                        Commands.TryAdd(field.Name, new MethodGroup(null, style, field, null, attribute.IsReadOnly));
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
                        NameToProperty.TryAdd(property.Name, property);

                        Commands.TryAdd(property.Name, new MethodGroup(null, NameToGroup[attribute.Group], null, property, attribute.IsReadOnly));
                    }
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }


            }
        }
    }


    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
        public class DebugVariableAttribute : System.Attribute {
            public string Group { get; }
            public bool IsReadOnly { get; }
            public DebugVariableAttribute (string group, bool isReadOnly = false) {
                Group = group;
                IsReadOnly = isReadOnly;
            }

            public DebugVariableAttribute (bool isReadOnly) {
                Group = HolyDebugGroupStyles.Uncategorized;
                IsReadOnly = isReadOnly;
            }

            public DebugVariableAttribute() {
                Group = HolyDebugGroupStyles.Uncategorized;
            }
        }
    }
