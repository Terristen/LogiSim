using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Collections;

namespace LogiSim
{
    public partial class UpdateConnectionRefractoryTime : SystemBase
    {

        protected override void OnUpdate()
        {

            var machinePortBufferLookup = GetBufferLookup<MachinePort>(false);

            Entities
                .WithNativeDisableParallelForRestriction(machinePortBufferLookup)
                .ForEach((Entity entity, int entityInQueryIndex, ref Machine machine) =>
                {
                    var portBuffer = machinePortBufferLookup[entity];

                    if (portBuffer.Length > 0)
                    {
                        for (int i = 0; i < portBuffer.Length; i++)
                        {
                            var connection = portBuffer[i];
                            connection.RefractoryTimer += SystemAPI.Time.DeltaTime;
                            portBuffer[i] = connection;
                        }
                    }
                }).ScheduleParallel();

        }
    }
}
