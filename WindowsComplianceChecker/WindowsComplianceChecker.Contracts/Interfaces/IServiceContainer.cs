namespace WindowsComplianceChecker.Contracts.Interfaces
{
    /// <summary>
    /// 輕量級依賴注入容器介面。
    /// 用於在 Composition Root（Program.cs）中集中完成服務註冊與解析，
    /// 實現各層之間的依賴注入（Dependency Injection）。
    /// </summary>
    public interface IServiceContainer
    {
        /// <summary>以類型對應方式註冊服務（每次 Resolve 建立新實例）</summary>
        void Register<TInterface, TImplementation>()
            where TImplementation : class, TInterface;

        /// <summary>以單例實例方式註冊服務</summary>
        void RegisterInstance<TInterface>(TInterface instance);

        /// <summary>解析已註冊的服務</summary>
        TInterface Resolve<TInterface>();
    }
}
