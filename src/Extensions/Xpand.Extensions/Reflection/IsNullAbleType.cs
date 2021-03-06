﻿using System;

namespace Xpand.Extensions.Reflection{
    public static partial class ReflectionExtensions{
        public static bool IsNullableType(this Type theType){
            return theType.IsGenericType && theType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}