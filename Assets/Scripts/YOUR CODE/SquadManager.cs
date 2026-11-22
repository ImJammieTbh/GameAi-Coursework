using System;
using UnityEngine;
using System.Collections.Generic;

namespace YOUR_CODE {
    public class SquadManager : MonoBehaviour {
        public List<AllyAgent> generals = new List<AllyAgent>();
        public List<bool> isFighting = new List<bool>();

        private void Start() {
            Initialise();
        }

        private void Update() {
            for(int i = 0; i < isFighting.Count; i++)
            {
                isFighting[i] = generals[i].IsFighting;
            }
        }

        private void Initialise() {
            GetGenerals();
            foreach (var general in generals) {
                isFighting.Add(false);
            }
        }

        private void GetGenerals() {
            generals.Clear();
            foreach (AllyAgent ally in GameData.Instance.allies) {
                if (ally is GeneralAgent) {
                    generals.Add(ally);
                }
            }
        }

        private void CallBackup() {
            
        }
    }
}

//TODO Add pathfinding for the generals and a case for if all generals die