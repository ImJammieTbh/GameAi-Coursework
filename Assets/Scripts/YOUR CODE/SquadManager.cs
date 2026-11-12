using System;
using UnityEngine;
using System.Collections.Generic;

namespace YOUR_CODE {
    public class SquadManager : MonoBehaviour {
        public List<SteeringAgent> generals = new List<SteeringAgent>();

        private void Start() {
            Initialise();
        }

        private void Initialise() {
            GetGenerals();
        }

        private void GetGenerals() {
            generals.Clear();
            foreach (SteeringAgent ally in GameData.Instance.allies) {
                if (ally is GeneralAgent) {
                    generals.Add(ally);
                }
            }
        }
    }
}

//TODO Add pathfinding for the generals and a case for if all generals die