using System;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace WizardParser
{
    class Program
    {
        static void Main(string[] args)
        {
            WebClient web = new WebClient();
            string nice_servant = web.DownloadString("https://api.atlasacademy.io/export/JP/nice_servant.json");
            string niceClassRelation = web.DownloadString("https://api.atlasacademy.io/export/JP/NiceClassRelation.json");
            string niceEnums = web.DownloadString("https://api.atlasacademy.io/export/JP/nice_enums.json");
            string niceClassAttack = web.DownloadString("https://api.atlasacademy.io/export/JP/NiceClassAttackRate.json");
            JArray servantData = JsonConvert.DeserializeObject<JArray>(nice_servant);
            JObject classRelation = JsonConvert.DeserializeObject<JObject>(niceClassRelation);
            JObject enums = JsonConvert.DeserializeObject<JObject>(niceEnums);
            JObject classAttack = JsonConvert.DeserializeObject<JObject>(niceClassAttack);


            List<Servant> servants = new List<Servant>();//lists of servant

            List<string> usableClassNames = className(classRelation);//gets a list of the classes in niceClassRelation

            //creates a dictionary for the classes
            Dictionary<string, int> classId = enums["SvtClass"].ToObject<Dictionary<int, string>>().ToDictionary(x => x.Value, x => x.Key);

            //creates a dictionary for the attributes
            Dictionary<string, int> attribute = enums["Attribute"].ToObject<Dictionary<int, string>>().ToDictionary(x => x.Value, x => x.Key);

            //creates a dictionary for their attacks
            Dictionary<string, int> attack = classAttack.ToObject<Dictionary<string, int>>();

            Data data = new Data();
            data.classRelation = CreateClassRelation(classRelation, usableClassNames, classId, attack);

            for (int i = 0; i < servantData.Count; ++i)
            {
                if ((int)servantData[i]["bondEquip"] != 0)
                {
                    servants.Add(new Servant());
                    assignData(servants[servants.Count - 1], servantData[i], classId, usableClassNames, data.classRelation, attribute);
                }
            }

            data.servants = servants;

            File.WriteAllText("data.json", JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        public static List<string> className(JObject classRelation)
        {
            Dictionary<string, int> temp = classRelation["saber"].ToObject<Dictionary<string, int>>();
            List<string> cn = new List<string>();
            foreach (string s in temp.Keys)
            {
                cn.Add(s);
            }
            return cn;
        }
        public static List<ClassRelation> CreateClassRelation(JObject classRelation, List<string> ucs, Dictionary<string, int> classId, Dictionary<string, int> attack)
        {
            List<ClassRelation> tempList = new List<ClassRelation>();

            for (int i = 0; i < ucs.Count; ++i)
            {
                var tempDic = new Dictionary<int, int>();
                for (int j = 0; j < ucs.Count; ++j)
                {
                    JObject t = (JObject)classRelation[ucs[j]];

                    tempDic.Add(classId[ucs[j]], t.ContainsKey(ucs[i]) ? (int)classRelation[ucs[i]][ucs[j]] : 1000);

                }
                tempList.Add(new ClassRelation(classId[ucs[i]], tempDic, attack[ucs[i]]));
            }

            return tempList;
        }
        public static void assignData(Servant s, JToken d, Dictionary<string, int> classNames, List<string> ucs, List<ClassRelation> baseClassRelation, Dictionary<string, int> attributeAffinity)
        {
            s.id = Convert.ToInt32(d["collectionNo"]);

            s.classId = classNames[(string)d["className"]];

            s.attributeId = attributeAffinity[(string)d["attribute"]];

            s.defaultLevelCap = Convert.ToInt32(d["lvMax"]);

            s.atkPerLevel = d["atkGrowth"].ToObject<int[]>();


            s.cardHitPercentages = new Dictionary<string, int[]>()
            {
                {"buster", d["hitsDistribution"]["buster"].ToObject<int[]>() },

                {"arts", d["hitsDistribution"]["arts"].ToObject<int[]>() },

                {"quick", d["hitsDistribution"]["quick"].ToObject<int[]>() },

                {"extra", d["hitsDistribution"]["extra"].ToObject<int[]>() }
            };

            s.hasDamagingNp = funcNametoBool(d["noblePhantasms"]);

            if (s.hasDamagingNp)
            {
                for(int i = 0; i < d["noblePhantasms"].Count(); ++i)
                {
                    if(Convert.ToInt32(d["noblePhantasms"][i]["strengthStatus"]) == 2)
                    {
                        s.npStrengthen.Add(npStruct(d["noblePhantasms"][i]));

                    }
                    else
                    {
                        s.nps.Add(npStruct(d["noblePhantasms"][i]));
                    }
                }
                if (s.npStrengthen.Count == 0) s.npStrengthen = null;

            }

            s.cardGen = new Dictionary<string, int>()
            {
                {"buster", (int)d["noblePhantasms"][0]["npGain"]["buster"][0] },
                {"arts", (int)d["noblePhantasms"][0]["npGain"]["arts"][0] },
                {"quick", (int)d["noblePhantasms"][0]["npGain"]["quick"][0] },
                {"extra", (int)d["noblePhantasms"][0]["npGain"]["extra"][0] }
            };

            s.passive = appendPassive(d["classPassive"], ucs, Convert.ToString(d["className"]), classNames, baseClassRelation);


            s.faceUrl = Convert.ToString(d["extraAssets"]["faces"]["ascension"]["4"]);

        }
        public static bool funcNametoBool(dynamic nps)
        {
            for (int i = 0; i < nps.Count; ++i)
            {
                for(int j = 0; j < nps[i]["functions"].Count; ++j)
                {
                    string f = nps[i]["functions"][j]["funcType"];

                    if (f.Length >= 8 &&f.Substring(0, 8) == "damageNp") return true;
                }
            }

            return false;
        }
        public static Np npStruct(dynamic np)
        {
            Np n = new Np();

                n.npCardType = np["card"];
                n.npGen = np["npGain"]["np"][0];
                n.npHitPercentages = np["npDistribution"].ToObject<int[]>();
                for (int j = 0; j < np["functions"].Count; ++j)
                {
                    string s = np["functions"][j]["funcType"];
                    if (s.Length >= 8 && s.Substring(0,8) == "damageNp")
                    {
                        for (int k = 0; k < 5; ++k)
                        {
                            n.mods[k] = np["functions"][j]["svals"][k]["Value"];
                        }
                    }
                }

            return n;
        }
        public static Passive appendPassive(JToken passives, List<string> ucs, string servantClass, Dictionary<string, int> classNamesDic, List<ClassRelation> baseClassRelation)
        {
            Passive passive = new Passive();
            for(int i = 0; i < passives.Count(); ++i)
            {
                for(int j = 0; j < passives[i]["functions"].Count(); ++j)
                {
                    if((int)passives[i]["functions"][j]["funcId"] != 0)
                    {

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "upCommandall")
                        {
                            int val = (int)passives[i]["functions"][j]["svals"][0]["Value"];
                            switch (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["ckSelfIndv"][0]["name"]))
                            {
                                case "cardQuick":
                                    {
                                        passive.quickMod = val;
                                    }
                                    break;
                                case "cardArts":
                                    {
                                        passive.artsMod = val;
                                    }
                                    break;
                                case "cardBuster":
                                    {
                                        passive.busterMod = val;
                                    }
                                    break;
                            }
                        }////quick arts buster mod

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "upCriticaldamage")
                        {
                            int val = (int)passives[i]["functions"][j]["svals"][0]["Value"];
                            if(passives[i]["functions"][j]["buffs"][0]["ckSelfIndv"].Count() != 0)
                            {
                                switch (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["ckSelfIndv"][0]["name"]))
                                {
                                    case "cardQuick":
                                        {
                                            passive.critDamageMod["quick"] = val;
                                        }
                                        break;
                                    case "cardArts":
                                        {
                                            passive.critDamageMod["buster"] = val;
                                        }
                                        break;
                                    case "cardBuster":
                                        {
                                            passive.critDamageMod["buster"] = val;
                                        }
                                        break;
                                    default:
                                        {
                                            passive.critDamageMod["quick"] = val;
                                            passive.critDamageMod["arts"] = val;
                                            passive.critDamageMod["buster"] = val;
                                        }
                                        break;
                                }
                            }

                        }//crit mod

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "upNpDamage")
                        {
                            int val = (int)passives[i]["functions"][j]["svals"][0]["Value"];
                            passive.npMod = val;
                        }//np mod

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "addDamage")
                        {
                            int val = (int)passives[i]["functions"][j]["svals"][0]["Value"];
                            passive.flatDamage = val;
                        }//flat damage

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "upDropnp")
                        {
                            int val = (int)passives[i]["functions"][j]["svals"][0]["Value"];
                            passive.npGen = val;
                        }//np gain

                        if (Convert.ToString(passives[i]["functions"][j]["buffs"][0]["type"]) == "overwriteClassRelation")
                        {

                            Dictionary<int, int> currentClassAdvantage = null;
                            foreach (ClassRelation cr in baseClassRelation)
                            {
                                if (cr.Id == classNamesDic[servantClass]) currentClassAdvantage = cr.ClassAdvantage;
                            }
                            for (int k = 0; k < ucs.Count; ++k)
                            {
                                JObject t = (JObject)passives[i]["functions"][j]["buffs"][0]["script"]["relationId"]["atkSide"][servantClass];
                                currentClassAdvantage[classNamesDic[ucs[k]]] = t.ContainsKey(ucs[k]) ? (int)t[ucs[k]]["damageRate"] : currentClassAdvantage[classNamesDic[ucs[k]]];

                            }
                            passive.classOverride = currentClassAdvantage;
                        }//overwrite class affinity
                    }
                }
            }
            return passive;
        }
    }
}
