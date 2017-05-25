using Common.Application;
using Common.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    public abstract class LicensedPlugin : InitializablePlugin, ILicensedPlugin
    {
        public override void Initialize(string name, params string[] args)
        {
            IsInitialized = true;
            Name = name;
            Arguments = args;
            var mgr = AppContext.Current.Container.GetInstance<ILicenseManager>();
            if (mgr == null)
            {
                AppContext.Current.Container.ContainerChanged += Shell_PluginChanged;
            }
            else
            {
                CheckLicensing(mgr.GetLicenses(), args);
            }
        }

        void Shell_PluginChanged(object sender, ContainerChangedEventArgs e)
        {
            if (e.UpdateType == ContainerUpdateType.Add
                && e.ServiceType == typeof(ILicenseManager))
            {
                CheckLicensing(AppContext.Current.Container.GetInstance<ILicenseManager>().GetLicenses(), this.Arguments);
            }
        }

        bool _isLicensingChecked = false;
        protected void CheckLicensing(ILicense[] licenses, params string[] args)
        {
            if (_isLicensingChecked) return;
            _isLicensingChecked = true;

            ApplyLicensing(licenses, args);
            IsEnabled = IsLicensed(this);
            if (IsEnabled)
            {
                IsInitialized = OnInitialize(args);
            }
        }

        public void ApplyLicensing(ILicense[] licenses, params string[] args)
        {
            OnApplyLicensing(licenses, args);
        }

        protected abstract void OnApplyLicensing(ILicense[] licenses, params string[] args);

        public bool IsLicensed(object plugin)
        {
            return OnIsLicensed(plugin);
        }

        protected abstract bool OnIsLicensed(object plugin);
    }
}
