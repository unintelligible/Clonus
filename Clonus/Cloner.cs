using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Clonus
{
    /*
    public interface ICloner {
		T Clone<T>(T source);
		T Clone<T>(T source);
	}
    */

    public static class Cloner
    {
        private static readonly Dictionary<ClonerKey, Delegate> _cloners = new Dictionary<ClonerKey, Delegate>();
        private static readonly Type DictionaryType = typeof(Dictionary<object, object>);

        public static T Clone<T>(T source)
            where T : class
        {
            return Clone(source, CloneMethod.Shallow);
        }

        public static T Clone<T>(T source, CloneMethod cloneMethod)
            where T : class
        {
            return Clone<T>(source, cloneMethod, new Dictionary<object, object>());
        }

        public static T Clone<T>(T source, CloneMethod cloneMethod, Dictionary<object, object> clonedObjects)
            where T : class
        {
            if (source == null)
                return default(T);
            var k = new ClonerKey { SourceType = source.GetType(), CloneMethod = cloneMethod };
            Delegate cloner;
            if (!_cloners.ContainsKey(k))
            {
                cloner = BuildCloner(k);
                _cloners[k] = cloner;
            }
            else
            {
                cloner = _cloners[k];
            }
            //return ((Func<T, T>)cloner)(source);
            if (clonedObjects.ContainsKey(source))
                return (T)clonedObjects[source];
            return (T)cloner.DynamicInvoke(source, clonedObjects);
            //clonedObjects[source] = clone;
            //return clone;
        }

        private static Delegate BuildCloner(ClonerKey clonerKey)
        {
            var t = clonerKey.SourceType;
            var dynamicMethod = new DynamicMethod("Clone_" + clonerKey.CloneMethod, t, new[] { t, DictionaryType }, Assembly.GetExecutingAssembly().ManifestModule, true);
            ILGenerator ilGenerator = dynamicMethod.GetILGenerator();

            if (t.IsValueType || t == typeof(string))
            {
                ilGenerator.Emit(OpCodes.Ldarg_0);
            }
            else if (t.GetInterfaces().Any(i => i == typeof(ICloneable)))
            {
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Callvirt, t.GetMethod("Clone"));
                ilGenerator.Emit(OpCodes.Castclass, t);
            }
            else
            {
                ConstructorInfo constructorInfo = t.GetConstructor(Type.EmptyTypes);
                if (constructorInfo == null)
                    throw new ArgumentException("Type " + t.FullName + " doesn't have a parameterless constructor - Clonus can't clone a type that doesn't have a parameterless constructor.");
                var localClone = ilGenerator.DeclareLocal(t);
                var localCounter = ilGenerator.DeclareLocal(typeof(int));
                var localClone2 = ilGenerator.DeclareLocal(t);
                var localBool = ilGenerator.DeclareLocal(typeof(bool));
                ilGenerator.Emit(OpCodes.Newobj, constructorInfo);
                ilGenerator.Emit(OpCodes.Stloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldloc_0);
                var addMethodInfo = DictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == "Add");
                ilGenerator.Emit(OpCodes.Callvirt, addMethodInfo);

                // copy public and private fields
                foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    EmitCloneField(clonerKey, fi, ilGenerator);
                var baseType = t.BaseType;
                while(baseType != null && baseType != typeof(object))
                {
                    foreach (var fi in baseType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        EmitCloneField(clonerKey, fi, ilGenerator);
                    baseType = baseType.BaseType;
                }
                // copy public properties
                /*
                foreach(var pi in t.GetProperties())
                {
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Callvirt, t.GetMethod("get_" + pi.Name));
                    ilGenerator.Emit(OpCodes.Callvirt, t.GetMethod("set_" + pi.Name));
				
                }
                 */
                ilGenerator.Emit(OpCodes.Ldloc_0);
            }
            ilGenerator.Emit(OpCodes.Ret);

            var funcType = typeof(Func<,,>);
            funcType = funcType.MakeGenericType(t, DictionaryType, t);

            var ret = dynamicMethod.CreateDelegate(funcType);
            return ret;
        }

        private static void EmitCloneField(ClonerKey clonerKey, FieldInfo fieldInfo, ILGenerator ilGenerator)
        {
            var fieldType = fieldInfo.FieldType;
            if (clonerKey.CloneMethod == CloneMethod.Shallow || fieldInfo.FieldType.IsValueType || fieldInfo.FieldType == typeof(string))
            {
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
            }
            else if (fieldType.GetInterfaces().Any(i => i == typeof(ICloneable)) && !fieldType.IsArray)
            {
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Callvirt, fieldType.GetMethod("Clone"));
                ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
            }
            else if (fieldType.IsArray)
            {
                // TODO: this doesn't check whether the array is null
                // check the array field isn't null
                var nullCheckLabel = ilGenerator.DefineLabel();
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldnull);
                ilGenerator.Emit(OpCodes.Ceq);
                ilGenerator.Emit(OpCodes.Stloc_3);
                ilGenerator.Emit(OpCodes.Ldloc_3);
                ilGenerator.Emit(OpCodes.Brtrue_S, nullCheckLabel);
                // initialise the array property on the target
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldlen);
                ilGenerator.Emit(OpCodes.Conv_I4);
                ilGenerator.Emit(OpCodes.Newarr, fieldType.GetElementType());
                ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
                // check the array length > 0
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldlen);
                ilGenerator.Emit(OpCodes.Conv_I4);
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Cgt);
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Ceq);
                ilGenerator.Emit(OpCodes.Stloc_3);
                ilGenerator.Emit(OpCodes.Ldloc_3);
                ilGenerator.Emit(OpCodes.Brtrue_S, nullCheckLabel);

                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Stloc_1);
                // start looping through each element in the array
                var forLoopLabel = ilGenerator.DefineLabel();
                ilGenerator.MarkLabel(forLoopLabel);
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldloc_1);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldloc_1);
                var elementType = fieldType.GetElementType();
                if (!elementType.IsValueType)
                {
                    // call GetClone() recursively
                    ilGenerator.Emit(OpCodes.Ldelem_Ref);
                    ilGenerator.Emit(OpCodes.Ldc_I4_1);
                    ilGenerator.Emit(OpCodes.Ldarg_1);
                    var mi = typeof(Cloner).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "Clone" && m.GetParameters().Length == 3);
                    mi = mi.MakeGenericMethod(new[] { fieldType.GetElementType() });
                    ilGenerator.Emit(OpCodes.Call, mi); 
                    ilGenerator.Emit(OpCodes.Stelem_Ref);
                } 
                else 
                {
                    // just copy the values over
                    if (elementType == typeof(int))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_I4);
                        ilGenerator.Emit(OpCodes.Stelem_I4);
                    }
                    else if (elementType == typeof(float))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_R4);
                        ilGenerator.Emit(OpCodes.Stelem_R4);
                    }
                    else if (elementType == typeof(double))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_R8);
                        ilGenerator.Emit(OpCodes.Stelem_R8);
                    }
                    else if (elementType == typeof(long))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_I8);
                        ilGenerator.Emit(OpCodes.Stelem_I8);
                    }
                    else if (elementType == typeof(short))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_I4);
                        ilGenerator.Emit(OpCodes.Stelem_I4);
                    }
                    else if (elementType == typeof(char))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_I2);
                        ilGenerator.Emit(OpCodes.Stelem_I2);
                    }
                    else if (elementType == typeof(byte))
                    {
                        ilGenerator.Emit(OpCodes.Ldelem_I1);
                        ilGenerator.Emit(OpCodes.Stelem_I1);
                    }
                    else
                    {
                        ilGenerator.Emit(OpCodes.Ldelem, elementType);
                        ilGenerator.Emit(OpCodes.Stelem, elementType);
                    }
                }
                ilGenerator.Emit(OpCodes.Ldloc_1);
                ilGenerator.Emit(OpCodes.Ldc_I4_1);
                ilGenerator.Emit(OpCodes.Add);
                ilGenerator.Emit(OpCodes.Stloc_1);
                ilGenerator.Emit(OpCodes.Ldloc_1);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldlen);
                ilGenerator.Emit(OpCodes.Conv_I4);
                ilGenerator.Emit(OpCodes.Clt);
                ilGenerator.Emit(OpCodes.Stloc_3);
                ilGenerator.Emit(OpCodes.Ldloc_3);
                ilGenerator.Emit(OpCodes.Brtrue_S, forLoopLabel);
                ilGenerator.MarkLabel(nullCheckLabel);
            }
            else
            {
                // call Cloner.GetClone recursively
                var cloneMethodInfo = typeof(Cloner).GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "Clone" && m.GetParameters().Length == 3);
                cloneMethodInfo = cloneMethodInfo.MakeGenericMethod(new[] { fieldType });
                var ifChecksFinishedLabel = ilGenerator.DefineLabel();
                if (fieldInfo.FieldType == fieldInfo.DeclaringType)
                {
                    // handle possible circular ref
                    var notCircularRefLabel = ilGenerator.DefineLabel();
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                    ilGenerator.Emit(OpCodes.Brfalse_S, notCircularRefLabel);
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                    ilGenerator.Emit(OpCodes.Ldarg_0);
                    ilGenerator.Emit(OpCodes.Ceq);
                    // start ?
                    /*
                        IL_0018: ldc.i4.0
                    	IL_0019: ceq
                    	IL_001b: br.s IL_001e
                    	IL_001d: ldc.i4.1
                    	IL_001e: stloc.2
                    	IL_001f: ldloc.2
                    	IL_0020: brtrue.s IL_002b
                     */
                    // end ?
                    ilGenerator.Emit(OpCodes.Brfalse_S, notCircularRefLabel);
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                    ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
                    ilGenerator.Emit(OpCodes.Br_S, ifChecksFinishedLabel); // added
                    ilGenerator.MarkLabel(notCircularRefLabel);
                }
                // check if we've already cloned this object
                var notAlreadyClonedLabel = ilGenerator.DefineLabel();
                var containsKeyMethodInfo = DictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == "ContainsKey");
                var getItemMethodInfo = DictionaryType.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(m => m.Name == "get_Item");
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Brfalse_S, notAlreadyClonedLabel);
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Callvirt, containsKeyMethodInfo);
                ilGenerator.Emit(OpCodes.Ldc_I4_0);
                ilGenerator.Emit(OpCodes.Ceq);
                // start ?
                var unknownLabel1 = ilGenerator.DefineLabel();
                ilGenerator.Emit(OpCodes.Br_S, unknownLabel1); 
                ilGenerator.Emit(OpCodes.Ldc_I4_1);
                ilGenerator.MarkLabel(unknownLabel1);
                ilGenerator.Emit(OpCodes.Stloc_2);
                ilGenerator.Emit(OpCodes.Ldloc_2);
                ilGenerator.Emit(OpCodes.Brtrue_S, notAlreadyClonedLabel);
                // end ?
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Callvirt, getItemMethodInfo);
                ilGenerator.Emit(OpCodes.Castclass, fieldInfo.FieldType);
                ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Br_S, ifChecksFinishedLabel); // added
                ilGenerator.MarkLabel(notAlreadyClonedLabel);

                // otherwise, call Cloner.GetClone recursively
                ilGenerator.Emit(OpCodes.Ldloc_0);
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldfld, fieldInfo);
                ilGenerator.Emit(OpCodes.Ldc_I4_1);
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Call, cloneMethodInfo);
                ilGenerator.Emit(OpCodes.Stfld, fieldInfo);
                ilGenerator.MarkLabel(ifChecksFinishedLabel);
            }
        }
    }
}