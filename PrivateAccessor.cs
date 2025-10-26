using System;
using System.Globalization;
using System.Reflection;
using ItemStatsSystem;

namespace Bank
{
    public static class PrivateAccessor
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 统一入口：优先属性（含私有 setter），其次自动属性后备字段，最后同名字段
        public static bool TrySet(object target, string memberName, object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));

            if (TrySetProperty(target, memberName, value)) return true;
            if (TrySetBackingField(target, memberName, value)) return true;
            if (TrySetField(target, memberName, value)) return true;
            return false;
        }

        // 新增：获取成员值（支持私有/受保护）
        public static bool TryGet(object target, string memberName, out object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));

            // 属性（含私有 getter）
            if (TryGetProperty(target, memberName, out value)) return true;
            // 自动属性后备字段
            if (TryGetBackingField(target, memberName, out value)) return true;
            // 同名字段
            if (TryGetField(target, memberName, out value)) return true;

            value = null;
            return false;
        }

        // 新增：强类型 TryGet
        public static bool TryGet<T>(object target, string memberName, out T value)
        {
            if (TryGet(target, memberName, out var boxed))
            {
                var nn = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                try
                {
                    if (boxed == null)
                    {
                        value = default;
                        return !nn.IsValueType; // 值类型取不到 null
                    }

                    if (nn.IsInstanceOfType(boxed))
                    {
                        value = (T)boxed;
                        return true;
                    }

                    value = (T)Convert.ChangeType(boxed, nn, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    value = default;
                    return false;
                }
            }

            value = default;
            return false;
        }

        private static bool TrySetProperty(object target, string propertyName, object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var prop = t.GetProperty(propertyName, Flags);
                if (prop == null) continue;

                var set = prop.GetSetMethod(true); // 私有 setter
                if (set == null) return false; // 只读属性，交由后备字段处理

                set.Invoke(target, new[] { ChangeType(value, prop.PropertyType) });
                return true;
            }
            return false;
        }

        // 自动属性的编译器后备字段：<PropertyName>k__BackingField
        private static bool TrySetBackingField(object target, string propertyName, object value)
        {
            var backing = "<" + propertyName + ">k__BackingField";
            return TrySetField(target, backing, value);
        }

        private static bool TrySetField(object target, string fieldName, object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(fieldName, Flags);
                if (field == null) continue;

                field.SetValue(target, ChangeType(value, field.FieldType));
                return true;
            }
            return false;
        }

        // 获取属性（含私有 getter）
        private static bool TryGetProperty(object target, string propertyName, out object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var prop = t.GetProperty(propertyName, Flags);
                if (prop == null) continue;

                // 排除带索引参数的属性
                var indexParams = prop.GetIndexParameters();
                if (indexParams != null && indexParams.Length > 0) break;

                var get = prop.GetGetMethod(true); // 私有 getter
                if (get == null) break;

                value = get.Invoke(target, null);
                return true;
            }

            value = null;
            return false;
        }

        // 获取自动属性后备字段
        private static bool TryGetBackingField(object target, string propertyName, out object value)
        {
            var backing = "<" + propertyName + ">k__BackingField";
            return TryGetField(target, backing, out value);
        }

        // 获取字段（含私有）
        private static bool TryGetField(object target, string fieldName, out object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(fieldName, Flags);
                if (field == null) continue;

                value = field.GetValue(target);
                return true;
            }

            value = null;
            return false;
        }

        private static object ChangeType(object value, Type targetType)
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var nn = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nn.IsInstanceOfType(value)) return value;

            return Convert.ChangeType(value, nn, CultureInfo.InvariantCulture);
        }
    }
}
