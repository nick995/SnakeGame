using GameWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;




namespace Server
{
    [DataContract(Namespace = "")]
    public class GameSettings
    {
        [DataMember(Name = "FramesPerShot")]        
        public long FramesPerShot { get; private set; }

        [DataMember(Name = "MSPerFrame")]
        public long MSPerFrame { get; private set; }

        [DataMember(Name = "RespawnRate")]
        public long RespawnRate { get; private set; }

        [DataMember(Name = "UniverseSize")]
        public int UniverseSize { get; private set; }

        [DataMember(Name = "Walls")]
        public List<Wall> Walls { get; private set; }        
    }
}
