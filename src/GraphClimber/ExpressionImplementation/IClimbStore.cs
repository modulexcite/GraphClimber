using System;

namespace GraphClimber
{
    internal interface IClimbStore
    {
        Action<object, T> GetSetter<T>(IStateMember member);
        Func<object, T> GetGetter<T>(IStateMember member);
        RouteDelegate GetRoute(IStateMember member, Type runtimeMemberType);
        ClimbDelegate<T> GetClimb<T>(Type runtimeType);
        Func<object,T> GetBoxGetter<T>(IStateMember member);
        Action<object, T> GetBoxSetter<T>(IStateMember member);
    }
}