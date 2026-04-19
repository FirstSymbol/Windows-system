namespace WindowsSystem.Resolver
{
  public interface IDependencyResolver
  {
    public T Resolve<T>();
  }
}