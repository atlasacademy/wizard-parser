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
            string nice_servant = web.DownloadString("https://api.atlasacademy.io/export/JP/nice_servant_lang_en.json");
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
                servants = servantData.Where(svt => svt.Value<string>("type") == "normal" || svt.Value<string>("type") == "heroine")
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

                    var t = (JObject)classRelation[firstClass];
                    tempDic.Add(
                        classId[secondClass],
                        t.ContainsKey(secondClass) ? (int)classRelation[firstClass][secondClass] : 1000
                    );
                }
                tempList.Add(new ClassRelation(classId[firstClass], tempDic, attack[firstClass]));
            }

            return tempList;
        }
        public static List<AttributeRelation> CreateAttributeRelation(JObject attributeRelation, Dictionary<string, int> attributeId)
        {
            var keys = attributeId.Keys.ToList().Except(new string[] { "void" });
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
        public static Servant GenerateServant(JToken svt, Dictionary<string, int> classNames, List<string> ucs, List<ClassRelation> baseClassRelation, Dictionary<string, int> attributeAffinity)
        {
            var s = new Servant
            {
                id = (int)svt["collectionNo"],
                classId = classNames[(string)svt["className"]],
                attributeId = attributeAffinity[(string)svt["attribute"]],
                defaultLevelCap = (int)svt["lvMax"],
                atkPerLevel = svt["atkGrowth"].ToObject<int[]>(),
                cardHitPercentages =
                    new Dictionary<string, int[]>()
                    {
                        { "buster", svt["hitsDistribution"]["buster"].ToObject<int[]>() },
                        { "arts", svt["hitsDistribution"]["arts"].ToObject<int[]>() },
                        { "quick", svt["hitsDistribution"]["quick"].ToObject<int[]>() },
                        { "extra", svt["hitsDistribution"]["extra"].ToObject<int[]>() }
                    },
                hasDamagingNp = funcNametoBool((JArray)svt["noblePhantasms"]),
                cardGen = new Dictionary<string, int>()
                {
                    { "buster", (int)svt["noblePhantasms"][0]["npGain"]["buster"][0] },
                    { "arts", (int)svt["noblePhantasms"][0]["npGain"]["arts"][0] },
                    { "quick", (int)svt["noblePhantasms"][0]["npGain"]["quick"][0] },
                    { "extra", (int)svt["noblePhantasms"][0]["npGain"]["extra"][0] }
                },
                passive = appendPassive(svt["classPassive"], ucs, svt["className"].ToString(), classNames,
                    baseClassRelation),
                faceUrl = (string)svt["extraAssets"]["faces"]["ascension"]["4"],
                skillMats = svt["skillMaterials"],
                appendMats = svt["appendSkillMaterials"],
                ascensionMats = svt["ascensionMaterials"]

            };


            if (s.hasDamagingNp)
            {
                for (int i = 0; i < svt["noblePhantasms"].Count(); ++i)
                {
                    var np = (JObject)svt["noblePhantasms"][i];
                    var n = npStruct(np);
                    var r = s.nps.FirstOrDefault(x => x.mods.SequenceEqual(n.mods));
                    if ((r is null) || (r is not null && r.npCardType != n.npCardType))
                    {
                        s.nps.Add(n);
                    }
                }
            }
            else if (s.id == 336)
            {
                s.hasDamagingNp = true;
                var web = new WebClient();
                string bazettNp = web.DownloadString("https://api.atlasacademy.io/nice/JP/NP/1001150");
                var n = npStruct(JsonConvert.DeserializeObject<JObject>(bazettNp));
                var r = s.nps.FirstOrDefault(x => x.mods.SequenceEqual(n.mods));
                if ((r is null) || (r is not null && r.npCardType != n.npCardType))
                {
                    s.nps.Add(n);
                }

            }
            //var excludedNps = new HashSet<int> { 101702, 402501, 402504 };
            //if ((int)svt["id"] == 200100)
            //{
            //    foreach (int npId in new List<int> { 200101, 200102, 200198, 200197 })
            //    {
            //        var np = (JObject)svt["noblePhantasms"].First(np => (int)np["id"] == npId);
            //        s.nps.Add(npStruct(np));
            //    }
            //}
            //else if (s.hasDamagingNp)
            //{
            //    for (int i = 0; i < svt["noblePhantasms"].Count(); ++i)
            //    {
            //        var np = (JObject)svt["noblePhantasms"][i];
            //        if ((string)np["name"] != "？？？" && !excludedNps.Contains((int)np["id"]))
            //        {
            //            s.nps.Add(npStruct(np));
            //        }
            //    }
            //}

            return s;
        }
        public static bool funcNametoBool(JArray nps)
        {
            foreach (var np in nps)
                foreach (var func in (JArray)np["functions"])
                    if (func["funcType"].ToString().StartsWith("damageNp")) return true;

            return false;
        }
        public static Np npStruct(JObject np)
        {
            var n = new Np
            {
                npCardType = (string)np["card"],
                strengthStatus = (int)np["strengthStatus"],
                priority = (int)np["priority"],
                npGen = (int)np["npGain"]["np"][0],
                npHitPercentages = np["npDistribution"].ToObject<int[]>()
            };
            foreach (var func in (JArray)np["functions"])
            {
                if (func["funcType"].ToString().StartsWith("damageNp"))
                {
                    n.mods = ((JArray)func["svals"]).Select(sval => sval.Value<int?>("Value") ?? default(int)).ToArray();
                    break;
                }
            }

            return n;
        }
        public static Passive appendPassive(JToken passives, List<string> ucs, string servantClass, Dictionary<string, int> classNamesDic, List<ClassRelation> baseClassRelation)
        {
            var _passive = new Passive();
            foreach (var passive in passives)
            {
                for (int j = 0; j < passive["functions"].Count(); ++j)
                {
                    if ((int)passive["functions"][j]["funcId"] != 0)
                    {
                        if (((JArray)passive["functions"][j]["buffs"]).Count == 0) continue;
                        string buffType = passive["functions"][j]["buffs"][0]["type"].ToString();

                        if (buffType == "upCommandall")
                        {
                            var val = (int)passive["functions"][j]["svals"][0]["Value"];
                            switch (passive["functions"][j]["buffs"][0]["ckSelfIndv"][0]["name"].ToString())
                            {
                                case "cardQuick":
                                    {
                                        _passive.quickMod += val;
                                    }
                                    break;
                                case "cardArts":
                                    {
                                        _passive.artsMod += val;
                                    }
                                    break;
                                case "cardBuster":
                                    {
                                        _passive.busterMod += val;
                                    }
                                    break;
                            }
                        }////quick arts buster mod

                        if (buffType == "upCriticaldamage")
                        {
                            var val = (int)passive["functions"][j]["svals"][0]["Value"];
                            if (passive["functions"][j]["buffs"][0]["ckSelfIndv"].Count() != 0)
                            {
                                switch (passive["functions"][j]["buffs"][0]["ckSelfIndv"][0]["name"].ToString())
                                {
                                    case "cardQuick":
                                        {
                                            _passive.critDamageMod["quick"] += val;
                                        }
                                        break;
                                    case "cardArts":
                                        {
                                            _passive.critDamageMod["arts"] += val;
                                        }
                                        break;
                                    case "cardBuster":
                                        {
                                            _passive.critDamageMod["buster"] += val;
                                        }
                                        break;
                                    default:
                                        {
                                            _passive.critDamageMod["quick"] += val;
                                            _passive.critDamageMod["arts"] += val;
                                            _passive.critDamageMod["buster"] += val;
                                        }
                                        break;
                                }
                            }
                            else
                            {
                                _passive.critDamageMod["quick"] += val;
                                _passive.critDamageMod["arts"] += val;
                                _passive.critDamageMod["buster"] += val;
                            }

                        }//crit mod

                        if (buffType == "upNpdamage")
                        {
                            _passive.npMod += (int)passive["functions"][j]["svals"][0]["Value"];
                        }//np mod

                        if (buffType == "addDamage")
                        {
                            _passive.flatDamage += (int)passive["functions"][j]["svals"][0]["Value"];
                        }//flat damage

                        if (buffType == "upDropnp")
                        {
                            _passive.npGen += (int)passive["functions"][j]["svals"][0]["Value"];
                        }//np gain

                        if (buffType == "overwriteClassRelation")
                        {

                            var currentClassAdvantage = new Dictionary<int, int> { };
                            var baseClassAdvantage = new Dictionary<int, int> { };
                            foreach (var cr in baseClassRelation)
                            {
                                if (cr.Id == classNamesDic[servantClass]) baseClassAdvantage = cr.ClassAdvantage;
                            }
                            for (int k = 0; k < ucs.Count; ++k)
                            {
                                var t = (JObject)passive["functions"][j]["buffs"][0]["script"]["relationId"]["atkSide"][servantClass];
                                currentClassAdvantage[classNamesDic[ucs[k]]] = t.ContainsKey(ucs[k]) ? (int)t[ucs[k]]["damageRate"] : baseClassAdvantage[classNamesDic[ucs[k]]];

                            }
                            _passive.classOverride = currentClassAdvantage;
                        }//overwrite class affinity
                    }
                }
            }
            return _passive;
        }
    }
}
