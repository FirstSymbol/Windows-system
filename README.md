<span style="font-family:monospace;">

# Windows System
## Installation
1. Install all dependencies plugins:
   - [UniTask](https://github.com/Cysharp/UniTask);
   - [TriInspector](https://github.com/codewriter-packages/Tri-Inspector);
   - [ExtDebugLogger](https://github.com/FirstSymbol/ExtDebugLogger).
2. Download package
   - From package manager - https://github.com/FirstSymbol/Windows-system.git
   - From file - download and drop in project folder. 

## Examples

### Initialization
1. Создать резолвер зависимостей передав.
2. Зарегистрировать сервис окон как новый 

### Basic use
``

# Documentation
## Dependency injection
В плагине начиная с версии `0.2.0` реализован интерфейс для инжекции зависимостей, чтобы этот процесс не требовал какого-то определенного плагина.

### IDependencyResolver.cs
```csharp
namespace WindowsSystem.Resolver
{
  public interface IDependencyResolver
  {
    T Resolve<T>();
  }
}
```
В интерфейсе есть только 1 метод, который возвращает запрашиваемый объект.</br>
Также есть уже готовый класс резолвера зависимостей.
```csharp
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
```

Как можно заметить, этот класс хранит ссылку на метод для решения зависимостей, который принимает тип `System.Type` и возвращает `object`, а далее в методе мы просто приводим тип `object` к запрашиваемому типу `T`.

### Примеры использования

`WindowsProvider` был зарегистрирован до регистрации сервиса окон и далее он автоматически через резолвер сам подтянется.

#### Extenject (Zenject)
```csharp
Container.BindInterfacesTo<WindowsService>().FromMethod( ctx =>
{
    var resolver = new DependencyResolver(Container.Resolve);
    return new WindowsService(resolver);
}).AsSingle().NonLazy();
```

#### VContainer
```csharp
builder.Register<IWindowsService>(container =>
{
    var resolver = new DependencyResolver(type => container.Resolve(type));
    return new WindowsService(resolver);
}, Lifetime.Singleton);
```

## Windows

---
### ExampleClassName

---
### Description
Some description `text`.
### Usage
```csharp
private int variable;
```

## Queue

---
### ExampleClassName

---
### Description
Some description `text`.
### Usage
```csharp
private int variable;
```
