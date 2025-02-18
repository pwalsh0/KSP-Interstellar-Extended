﻿using System.Linq;
using KSP.Localization;
using UnityEngine;

namespace FNPlugin.Science
{
    public class InterstellarResourceScienceModule : ModuleScienceExperiment
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_KSPIE_ResourceScience_Active")]//Active
        public bool generatorActive;
        [KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_KSPIE_ResourceScience_GeneratedData", guiFormat = "F3")]//Generated Data
        public double totalGeneratedData;

        [KSPField(isPersistant = true)] public double last_active_time;
        [KSPField(isPersistant = true)] public double lastGeneratedPerSecond;

        [KSPField] public double resourceAmount;
        [KSPField] public string resourceName;
        [KSPField] public double generatorResourceIn;
        [KSPField] public double generatorResourceOut;

        [KSPField(guiActive = false)] public string generatorResourceInName;
        [KSPField(guiActive = false)] public string generatorResourceOutName;
        [KSPField(guiActive = false)] public string generatorActivateName;
        [KSPField(guiActive = false)] public string generatorDeactivateName;

        [KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_KSPIE_ResourceScience_Biodome")]//Biodome
        public string currentBiome = "";

        [KSPField] public bool needSubjects = false;
        [KSPField] public string loopingAnimation = "";
        [KSPField] public int crewCount;
        [KSPField] public float loopPoint;

        [KSPEvent(guiName = "#LOC_KSPIE_ResourceScience_ActivateGenerator", active = true, guiActive = true)]//Activate Generator
        public void ActivateGenerator()
        {
            generatorActive = true;
            PlayAnimation("Deploy", false, false, false);
        }

        [KSPEvent(guiName = "#LOC_KSPIE_ResourceScience_deActivateGenerator", active = true, guiActive = true)]//Activate Generator
        public void DeActivateGenerator()
        {
            generatorActive = false;
            PlayAnimation("Deploy", true, true, false);
        }

        public override void OnStart(PartModule.StartState state)
        {
            Debug.Log("[KSPI]: InterstellarResourceScienceModule - OnStart " + state.ToString());

            //this.Events["Deploy"].guiActive = false;
            Events[nameof(ActivateGenerator)].guiName = generatorActivateName;
            Events[nameof(DeActivateGenerator)].guiName = generatorDeactivateName;

            PlayAnimation("Deploy", !generatorActive, true, false);

            base.OnStart(state);

            // calculate time past since last frame
            if (generatorActive && last_active_time > 0)
            {
                double timeDiff = Planetarium.GetUniversalTime() - last_active_time;

                var minutes = timeDiff / 60;
                Debug.Log("[KSPI]: InterstellarResourceScienceModule - time difference " + minutes + " minutes");
                ScreenMessages.PostScreenMessage(Localizer.Format("#LOC_KSPIE_ResourceScience_Postmsg", minutes.ToString("0.00")), 5.0f, ScreenMessageStyle.LOWER_CENTER);//"Generated Science Data for " +  + " minutes"

                GenerateScience(timeDiff, true);
            }
        }

        public override void OnUpdate()
        {
            // store current time in case vessel is unloaded
            last_active_time = Planetarium.GetUniversalTime();

            int lcrewCount = part.protoModuleCrew.Count;
            if (generatorActive)
            {
                if (loopPoint != 0) //loop the animation from this point, if 0, dont loop
                    PlayAnimation("Deploy", false, false, true);

                Events["deActivateGenerator"].guiActive = true;
                Events["activateGenerator"].guiActive = false;
                //while the generator is active... update the resource based on how much game time passed
                // print("part has crews!" + part.protoModuleCrew.Count.ToString());
                if ((part.protoModuleCrew.Count == part.CrewCapacity && needSubjects) || !needSubjects)
                {
                    // double budget = getResourceBudget(generatorResourceInName);
                    // print(budget.ToString());
                    // if (budget > 0)
                    //{
                    GenerateScience(TimeWarp.deltaTime, false);
                    //}
                }
            }
            else
            {
                Events["deActivateGenerator"].guiActive = false;
                Events["activateGenerator"].guiActive = true;
            }

            string biome = BiomeCheck();
            if (biome != currentBiome || (needSubjects && lcrewCount != crewCount))
            {
                if (biome != currentBiome)
                    Debug.Log("[KSPI]: InterstellarResourceScienceModule - reseting research because biome " + biome + " != biome " + currentBiome);
                else
                    Debug.Log("[KSPI]: InterstellarResourceScienceModule - reseting research because lcrewCount " + lcrewCount + " !=  crewCount " + crewCount);

                print("biome change " + biome);
                currentBiome = biome;
                crewCount = lcrewCount;
                //reset collected data

                if (!string.IsNullOrWhiteSpace(resourceName))
                    part.RequestResource(resourceName, resourceAmount);

            }
            if (loopingAnimation != "")
                PlayAnimation(loopingAnimation, false, false, true); //plays independently of other anims

            base.OnUpdate();
        }

        private void GenerateScience(double deltaTime, bool offlineCollecting)
        {
            if (deltaTime == 0) return;

            double spent;
            if (!offlineCollecting)
            {
                spent = part.RequestResource(generatorResourceInName, generatorResourceIn * deltaTime);
                lastGeneratedPerSecond = spent / deltaTime;
            }
            else
            {
                spent = lastGeneratedPerSecond * deltaTime;
                Debug.Log("[KSPI]: InterstellarResourceScienceModule - available power: " + spent);
            }

            //  print(spent.ToString());
            double generateScale = spent / (generatorResourceIn * deltaTime);
            if (generatorResourceIn == 0)
                generateScale = 1;

            var generatedScience = generatorResourceOut * deltaTime * generateScale;
            if (offlineCollecting)
            {
                Debug.Log("[KSPI]: InterstellarResourceScienceModule - generatedScience: " + generatedScience);
            }

            double generated = part.RequestResource(generatorResourceOutName, -generatedScience);

            totalGeneratedData = part.Resources[generatorResourceOutName].amount;

            //  print("generated " + generated.ToString());
            if (generated == 0 && !offlineCollecting) //if we didn't generate anything then we're full, refund the spent resource
                part.RequestResource(generatorResourceInName, -spent);
        }

        public string BiomeCheck()
        {
            // bool flying = vessel.altitude < vessel.mainBody.maxAtmosphereAltitude;
            //bool orbiting =

            //return "InspaceOver" + vessel.mainBody.name;

            string situation = vessel.RevealSituationString();
            if (situation.Contains("Landed") || situation.Contains("flight"))
                return FlightGlobals.currentMainBody.BiomeMap.GetAtt(vessel.latitude * Mathf.Deg2Rad, vessel.longitude * Mathf.Deg2Rad).name + situation;
            return situation;
        }

        double getResourceBudget(string name)
        {
            if (vessel != FlightGlobals.ActiveVessel)
                return 0;

            // print("found vessel event!");
            //var resources = vessel.GetActiveResources();
            var resources = vessel.parts.SelectMany(p => p.Resources).ToList();

            foreach (var t in resources)
            {
                // print("vessel has resources!");
                print(t.info.name);
                // print("im looking for " + resourceName);
                if (t.info.name == resourceName)
                {
                    // print("Found the resouce!!");
                    return t.amount;
                }
            }
            return 0;
        }

        bool VesselHasEnoughResource(string name, double rc)
        {
            if (rc <= 0)
                return true;

            //if (this.vessel == FlightGlobals.ActiveVessel)
            //{
                //print("found vessel event!");
                //var resources = vessel.GetActiveResources();
                var resources = vessel.parts.SelectMany(p => p.Resources).ToList();
                for (int i = 0; i < resources.Count; i++)
                {
                    //print("vessel has resources!");
                    print(resources[i].info.name);
                    //print("im looking for " + resourceName);
                    if (resources[i].info.name == name)
                    {
                        //print("Found the resouce!!");
                        if (resources[i].amount >= rc)
                        {
                            return true;
                        }
                    }
                }
            //}
            return false;
        }
        new public void DumpData(ScienceData data)
        {
            // refundResource();
            base.DumpData(data);
        }

        [KSPEvent(guiName = "#LOC_KSPIE_ResourceScience_Deploy", active = true, guiActive = true)]//Deploy
        new public void DeployExperiment()
        {
            //print("Clicked event! check data: " + resourceName + " " + resourceAmount.ToString() + " " + experimentID + " ");
            if (VesselHasEnoughResource(resourceName, resourceAmount))
            {
                //print("Has the possibleAmount!!");
                double res = part.RequestResource(resourceName, resourceAmount, ResourceFlowMode.ALL_VESSEL);
                //print("got " + res.ToString() + "resources");
                base.DeployExperiment();
                //  ReviewDataItem(data);
            }
            else
            {
                ScreenMessage smg = new ScreenMessage(Localizer.Format("#LOC_KSPIE_ResourceScience_Postmsg2"), 4.0f, ScreenMessageStyle.UPPER_LEFT);//"Not Enough Data Stored"
                ScreenMessages.PostScreenMessage(smg);
                print("not enough data stored");
            }
            //print("Deploying Experiment");
            //print("resourcename, resource possibleAmount " + resourceName + " " + resourceAmount.ToString());
        }

        [KSPAction("Deploy")]
        new public void DeployAction(KSPActionParam actParams)
        {
            //print("Clicked event! check data: " + resourceName + " " + resourceAmount.ToString() + " " + experimentID + " ");
            if (VesselHasEnoughResource(resourceName, resourceAmount))
            {
                //print("Has the possibleAmount!!");
                double res = part.RequestResource(resourceName, resourceAmount, ResourceFlowMode.ALL_VESSEL);
                //print("got " + res.ToString() + "resources");

                base.DeployAction(actParams);
                //  ReviewDataItem(data);
            }
            else
            {
                ScreenMessage smg = new ScreenMessage(Localizer.Format("#LOC_KSPIE_ResourceScience_Postmsg2"), 4.0f, ScreenMessageStyle.UPPER_LEFT);//"Not Enough Data Stored"
                ScreenMessages.PostScreenMessage(smg);
                print("not enough data stored");
            }
            //print("Deploying Experiment");
            //print("resourcename, resource possibleAmount " + resourceName + " " + resourceAmount.ToString());
        }

        //[KSPEvent(active = true, guiActive = true, guiName = "Review Data")]
        //new public void ReviewDataEvent()
        //{
        //    print("Reviewing Data");
        //    base.ReviewDataEvent();
        //}
        void refundResource()
        {
            print("refund resource!");
            double res = part.RequestResource(resourceName, -resourceAmount, ResourceFlowMode.ALL_VESSEL);
            print("refunded " + res + " resource");
        }

        //[KSPEvent(guiName = "Reset", active = true, guiActive = true)]
        //new public void ResetExperiment()
        //{
        //    // refundResource();
        //    base.ResetExperiment();
        //}

        //[KSPEvent(guiName = "Reset", active = true, guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = false)]
        //new public void ResetExperimentExternal()
        //{
        //    //  refundResource();
        //    base.ResetExperimentExternal();
        //}

        //[KSPAction("Reset")]
        //new public void ResetAction(KSPActionParam actParams)
        //{
        //    //refundResource();
        //    base.ResetAction(actParams);
        //}

        private void PlayStartAnimation(Animation StartAnimation, string startAnimationName, int speed, bool instant)
        {
            if (startAnimationName == "")
                return;

            if (speed < 0)
            {
                StartAnimation[startAnimationName].time = StartAnimation[startAnimationName].length;
                if (loopPoint != 0)
                    StartAnimation[startAnimationName].time = loopPoint;
            }
            if (instant)
                StartAnimation[startAnimationName].speed = 999999 * speed;

            StartAnimation[startAnimationName].wrapMode = WrapMode.Default;
            StartAnimation[startAnimationName].speed = speed;
            StartAnimation.Play(startAnimationName);
        }

        private void PlayLoopAnimation(Animation StartAnimation, string startAnimationName, int speed, bool instant)
        {
            if (startAnimationName == "") return;

            // print(StartAnimation[startAnimationName].time.ToString() + " " + loopPoint.ToString());
            if (StartAnimation[startAnimationName].time >= StartAnimation[startAnimationName].length || StartAnimation.isPlaying == false)
            {
                StartAnimation[startAnimationName].time = loopPoint;
                //print(StartAnimation[startAnimationName].time.ToString() + " " + loopPoint.ToString());
                if (instant)
                    StartAnimation[startAnimationName].speed = 999999 * speed;

                StartAnimation[startAnimationName].speed = speed;
                StartAnimation[startAnimationName].wrapMode = WrapMode.Default;
                StartAnimation.Play(startAnimationName);
            }
        }

        public void PlayAnimation(string name, bool rewind, bool instant, bool loop)
        {
            // note: assumes one ModuleAnimateGeneric (or derived version) for this part
            // if this isn't the case, needs fixing. That's cool, I called in the part.cfg

            var anim = part.FindModelAnimators();

            foreach (Animation a in anim)
            {
                // print("animation found " + a.name + " " + a.clip.name);
                if (a.clip.name != name)
                    continue;

                // print("animation playingxx " + a.name + " " + a.clip.name);
                var xanim = a;
                if (loop)
                    PlayLoopAnimation(xanim, name, (rewind) ? (-1) : (1), instant);
                else
                    PlayStartAnimation(xanim, name, (rewind) ? (-1) : (1), instant);
            }
        }
    }
}
