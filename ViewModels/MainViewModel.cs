using System.Collections.Generic;
using ESPWebBridge.Services;

namespace ESPWebBridge.ViewModels;

public partial class MainViewModel(IEnumerable<IManagedService> services) : ViewModelBase
{
    public ServicesViewModel ServicesModel { get; } = new(services);
}
