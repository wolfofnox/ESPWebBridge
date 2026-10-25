using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ESPWebBridge.Services;

namespace ESPWebBridge.ViewModels;

public partial class ServicesViewModel : ViewModelBase
{
    public ObservableCollection<ServiceViewModel> Services { get; }
    public ServicesViewModel(IEnumerable<IManagedService> services)
    {
        Services = new(services.Select(service => new ServiceViewModel(service)));
    }
}
