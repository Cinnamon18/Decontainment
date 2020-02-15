using System;
using UnityEngine;

namespace Bot
{
    public class Shooter : MonoBehaviour
    {
        public Trigger shotRequested;
        public bool async;

        public Hardpoint hardpoint;
        public WeaponData weaponData;

        private float cooldownTimer;

        public bool Running { get { return !async && cooldownTimer > 0; } }

        void FixedUpdate() {
            cooldownTimer -= Time.fixedDeltaTime;
            if (shotRequested.Value && cooldownTimer <= 0) {
                cooldownTimer = weaponData.cooldown;
                async = true;

                if (weaponData.numShots > 1) {
                    float offset = -(((float) weaponData.numShots - 1) / 2.0f) * weaponData.shotSpacing;

                    for (int i = 0; i < weaponData.numShots; i++)
                    {
                        Projectile.CreateProjectile(this, weaponData.projectilePrefab, hardpoint.transform.position, hardpoint.transform.right + new Vector3(Mathf.Cos(Mathf.Deg2Rad * (weaponData.shotSpacing * i + offset + hardpoint.transform.eulerAngles.z)),
                                                                                                                                               Mathf.Sin(Mathf.Deg2Rad * (weaponData.shotSpacing * i + offset + hardpoint.transform.eulerAngles.z)), 0));
                    }
                } else {
                    Projectile.CreateProjectile(this, weaponData.projectilePrefab, hardpoint.transform.position, hardpoint.transform.right);
                }
            }
        }

        public void Init(Hardpoint hardpoint, WeaponData weaponData) {
            this.hardpoint = hardpoint;
            this.weaponData = weaponData;

            this.hardpoint.Init(weaponData.hardpointColor);
        }
    }
}