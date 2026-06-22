using System;
using System.Collections.Generic;
using WindowsComplianceChecker.Contracts.Interfaces;

namespace WindowsComplianceChecker.BLL
{
    /// <summary>
    /// 輕量級依賴注入容器實作（BLL 層）。
    /// 在 Composition Root (Program.cs) 中完成所有服務的註冊，
    /// 各層只依賴介面，不依賴具體實作，達成依賴反轉原則 (DIP)。
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, Func<object>> _registrations
            = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// 以類型對應方式註冊服務。
        /// 每次呼叫 Resolve 時，透過 Activator.CreateInstance 建立新實例。
        /// </summary>
        public void Register<TInterface, TImplementation>()
            where TImplementation : class, TInterface
        {
            _registrations[typeof(TInterface)] =
                () => Activator.CreateInstance<TImplementation>();
        }

        /// <summary>
        /// 以單例實例方式註冊服務。
        /// 每次呼叫 Resolve 時回傳同一個物件實例。
        /// </summary>
        public void RegisterInstance<TInterface>(TInterface instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _registrations[typeof(TInterface)] = () => instance;
        }

        /// <summary>解析已註冊的服務，找不到時擲出 InvalidOperationException</summary>
        public TInterface Resolve<TInterface>()
        {
            if (_registrations.TryGetValue(typeof(TInterface), out var factory))
                return (TInterface)factory();

            throw new InvalidOperationException(
                $"服務 '{typeof(TInterface).Name}' 尚未在容器中註冊。");
        }
    }
}
