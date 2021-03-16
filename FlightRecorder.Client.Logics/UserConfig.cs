
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics
{
    public class UserConfigModel
    {
        public bool AutomaticUpdate { get; set; }
    }

    public class UserConfig : UserConfigModel
    {
      
        public  UserConfig ()
        {

            string path_file= System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + @"\UserConfig.Json"; 
            if(!File.Exists(path_file))
            {
                this.AutomaticUpdate = false;


                string values = JsonSerializer.Serialize<UserConfig>(this, null);
                using (StreamWriter writer = new(path_file))
                {
                    writer.Write(values);
                    writer.Close();
                }

            }
            else
            {
                string json_config = System.IO.File.ReadAllText(path_file);
                var tests= JsonSerializer.Deserialize<UserConfigModel>(json_config);
                this.AutomaticUpdate = tests.AutomaticUpdate;
            }
        }


        public void SetAutomaticUpdateValue(bool automatic)
        {
            this.AutomaticUpdate = automatic;
            string path_file = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) + @"\UserConfig.Json";
            string values = JsonSerializer.Serialize<UserConfig>(this, null);
            using (StreamWriter writer = new(path_file))
            {
                writer.Write(values);
                writer.Close();
            }
        }

    


    }

    
}
