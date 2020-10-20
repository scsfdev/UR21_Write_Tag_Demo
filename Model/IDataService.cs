using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UR21_Write_Tag_Demo.Model
{
    public interface IDataService
    {
        void GetData(Action<DataItem, Exception> callback);
    }
}
