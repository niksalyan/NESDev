using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dotNES.Controllers;
using dotNES.Mappers;

namespace dotNES
{
    class Emulator
    {
        private static readonly Dictionary<int, KeyValuePair<Type, MapperDef>> Mappers = (from type in Assembly.GetExecutingAssembly().GetTypes()
                                                                 let def = (MapperDef)type.GetCustomAttributes(typeof(MapperDef), true).FirstOrDefault()
                                                                 where def != null
                                                                 select new { def, type }).ToDictionary(a => a.def.Id, a => new KeyValuePair<Type, MapperDef>(a.type, a.def));


        public IController Controller;

        public readonly CPU CPU;

        public readonly PPU PPU;

        public readonly BaseMapper Mapper;

        public readonly Cartridge Cartridge;



        public Emulator(byte[] raw, IController controller)
        {
            Cartridge = new Cartridge(raw);
            if (!Mappers.ContainsKey(Cartridge.MapperNumber))
                throw new NotImplementedException($"unsupported mapper {Cartridge.MapperNumber}");
            Mapper = (BaseMapper)Activator.CreateInstance(Mappers[Cartridge.MapperNumber].Key, this);
            CPU = new CPU(this);
            PPU = new PPU(this);
            Controller = controller;

            Load();
        }

        public void Save()
        {   // TODO DO WE WANT SAVE
            /*using (var fs = new FileStream(_path + ".sav", FileMode.Create, FileAccess.Write))
            {
                Mapper.Save(fs);
            }*/
        }

        public void Load()
        {
            /*var sav = _path + ".sav";
            if (!File.Exists(sav)) return;

            using (var fs = new FileStream(sav, FileMode.Open, FileAccess.Read))
            {
                Mapper.Load(fs);
            }*/
        }
    }
}
