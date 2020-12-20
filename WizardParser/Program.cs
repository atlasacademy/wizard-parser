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
        static void Main()
        {
            WebClient web = new WebClient();
            string nice_servant = web.DownloadString("https://api.atlasacademy.io/export/JP/nice_servant.json");
            string niceClassRelation = web.DownloadString("https://api.atlasacademy.io/export/JP/NiceClassRelation.json");
            string niceEnums = web.DownloadString("https://api.atlasacademy.io/export/JP/nice_enums.json");
            string niceClassAttack = web.DownloadString("https://api.atlasacademy.io/export/JP/NiceClassAttackRate.json");
            string niceAttributeRelation = web.DownloadString("https://api.atlasacademy.io/export/JP/NiceAttributeRelation.json");
            JArray servantData = JsonConvert.DeserializeObject<JArray>(nice_servant);
            JObject classRelation = JsonConvert.DeserializeObject<JObject>(niceClassRelation);
            JObject enums = JsonConvert.DeserializeObject<JObject>(niceEnums);
            JObject classAttack = JsonConvert.DeserializeObject<JObject>(niceClassAttack);
            JObject attributeRelation = JsonConvert.DeserializeObject<JObject>(niceAttributeRelation);

            var usableClassNames = className(classRelation);//gets a list of the classes in niceClassRelation

            //creates a dictionary for the classes
            var classId = enums["SvtClass"].ToObject<Dictionary<int, string>>().ToDictionary(x => x.Value, x => x.Key);

            //creates a dictionary for the attributes
            var attribute = enums["Attribute"].ToObject<Dictionary<int, string>>().ToDictionary(x => x.Value, x => x.Key);

            //creates a dictionary for their attacks
            var attack = classAttack.ToObject<Dictionary<string, int>>();

            var classRelationList = CreateClassRelation(classRelation, usableClassNames, classId, attack);
            var data = new Data
            {
                classRelation = classRelationList,
                attributeRelation = CreateAttributeRelation(attributeRelation, attribute),
                servants = servantData.Where(svt => svt.Value<int?>("bondEquip") != 0)
                    .Select(svt => GenerateServant(svt, classId, usableClassNames, classRelationList, attribute))
                    .ToList()
            };

            File.WriteAllText("data.json", JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        public static List<string> className(JObject classRelation)
        {
            return classRelation["saber"].ToObject<Dictionary<string, int>>().Keys.ToList();
        }
        public static List<ClassRelation> CreateClassRelation(JObject classRelation, List<string> usableClassNames, Dictionary<string, int> classId, Dictionary<string, int> attack)
        {
            var tempList = new List<ClassRelation>();
            foreach (var firstClass in usableClassNames)
            {
                var tempDic = new Dictionary<int, int>();
                foreach (var secondClass in usableClassNames)
                {
                    var t = (JObject)classRelation[secondClass];
                    tempDic.Add(
                        classId[secondClass],
                        t.ContainsKey(firstClass) ? (int)classRelation[firstClass][secondClass] : 1000
                    );
                }
                tempList.Add(new ClassRelation(classId[firstClass], tempDic, attack[firstClass]));
            }

            return tempList;
        }
        public static List<AttributeRelation> CreateAttributeRelation(JObject attributeRelation, Dictionary<string, int> attributeId)
        {
            var keys = attributeId.Keys.ToList();
            var tempList = new List<AttributeRelation>();

            foreach (var attackerAttribute in keys)
            {
                var tempDic = new Dictionary<int, int>();
                foreach (var defenderAttribute in keys)
                {
                    var t = (JObject)attributeRelation[defenderAttribute];
                    tempDic.Add(attributeId[defenderAttribute], t.ContainsKey(attackerAttribute) ? (int)attributeRelation[attackerAttribute][defenderAttribute] : 1000);
                }
                tempList.Add(new AttributeRelation(attributeId[attackerAttribute], tempDic));
            }
            return tempList;
        }
        public static Servant GenerateServant(JToken d, Dictionary<string, int> classNames, List<string> ucs, List<ClassRelation> baseClassRelation, Dictionary<string, int> attributeAffinity)
        {
            var s = new Servant
            {
                id = (int) d["collectionNo"],
                classId = classNames[(string) d["className"]],
                attributeId = attributeAffinity[(string) d["attribute"]],
                defaultLevelCap = (int) d["lvMax"],
                atkPerLevel = d["atkGrowth"].ToObject<int[]>(),
                cardHitPercentages =
                    new Dictionary<string, int[]>()
                    {
                        {"buster", d["hitsDistribution"]["buster"].ToObject<int[]>()},
                        {"arts", d["hitsDistribution"]["arts"].ToObject<int[]>()},
                        {"quick", d["hitsDistribution"]["quick"].ToObject<int[]>()},
                        {"extra", d["hitsDistribution"]["extra"].ToObject<int[]>()}
                    },
                hasDamagingNp = funcNametoBool(d["noblePhantasms"]),
                cardGen = new Dictionary<string, int>()
                {
                    {"buster", (int) d["noblePhantasms"][0]["npGain"]["buster"][0]},
                    {"arts", (int) d["noblePhantasms"][0]["npGain"]["arts"][0]},
                    {"quick", (int) d["noblePhantasms"][0]["npGain"]["quick"][0]},
                    {"extra", (int) d["noblePhantasms"][0]["npGain"]["extra"][0]}
                },
                passive = appendPassive(d["classPassive"], ucs, Convert.ToString(d["className"]), classNames,
                    baseClassRelation),
                faceUrl = (string) d["extraAssets"]["faces"]["ascension"]["4"]
            };


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

            return s;
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
