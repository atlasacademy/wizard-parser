using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace WizardParser
{
    public class ClassRelation
    {
        public ClassRelation(int id, Dictionary<int, int> ca, int attack)
        {
            this.Id = id;
            this.ClassAdvantage = ca;
            this.Attack = attack;
        }
        public int Id { get; set; }
        public int Attack { get; set; }
        public Dictionary<int, int> ClassAdvantage { get; set; }

    }
    public class AttributeRelation
    {
        public AttributeRelation(int id, Dictionary<int, int> aa)
        {
            this.Id = id;
            this.AttributeAdvantage = aa;
        }
        public int Id { get; set; }
        public Dictionary<int, int> AttributeAdvantage { get; set; }
    }
    public class Data
    {
        public List<ClassRelation> classRelation { get; set; }
        public List<AttributeRelation> attributeRelation { get; set; }
        public List<Servant> servants;

    }
    public class Servant
    {
        public int id;
        public int classId;
        public int attributeId;
        public int defaultLevelCap;
        public int[] atkPerLevel;
        public Dictionary<string, int[]> cardHitPercentages = new Dictionary<string, int[]>();
        public bool hasDamagingNp;

        public List<Np> nps = new List<Np>();
        public JToken skillMats;
        public JToken ascensionMats;
        public Dictionary<string, int> cardGen;
        public Passive passive = new Passive();
        public bool hasTrait2632 = false;
        public string faceUrl;
        
    }

    public class Np
    {
        public string npCardType;
        public int strengthStatus;
        public int priority;
        public int[] npHitPercentages;
        public int npGen;
        public int[] mods = new int[5];
    }
    public class Passive
    {
        public int quickMod;
        public int busterMod;
        public int artsMod;
        public int flatDamage;
        public Dictionary<string, int> critDamageMod;
        public int npGen;
        public int npMod;
        public Dictionary<int, int> classOverride = null;
        public Passive()
        {
            quickMod = 0;
            busterMod = 0;
            artsMod = 0;
            flatDamage = 0;
            critDamageMod = new Dictionary<string, int>
            {
                {"buster",0 },
                {"quick",0 },
                {"arts",0 }
            };
            npGen = 0;
            npMod = 0;
        }
    }

}
