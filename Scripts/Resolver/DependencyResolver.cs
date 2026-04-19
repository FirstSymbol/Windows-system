using System;

namespace WindowsSystem.Resolver
{
  public class DependencyResolver : IDependencyResolver
  {
    private readonly Func<Type, object> _resolver;
    public DependencyResolver(Func<Type, object> resolver)
    {
      _resolver = resolver;
    }

    public T Resolve<T>() => (T)_resolver(typeof(T));
  }
}