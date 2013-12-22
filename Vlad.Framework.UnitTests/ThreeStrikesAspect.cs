﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Value.Framework.Aspectacular;

namespace Value.Framework.UnitTests
{
    /// <summary>
    /// Makes three attempts to call intercepted method if it throws and exception.
    /// </summary>
    public class ThreeStrikesAspect : Aspect
    {
        public override void Step_4_Optional_AfterCatchingMethodExecException()
        {
            if (this.Context.AttemptsMade < 3)
            {
                this.Context.ShouldRetryCall = true;
                this.LogInformationWithKey("Failed call retry", "{0} attempt will be made due to \"{1}\".",  this.Context.AttemptsMade+1, this.Context.MethodExecutionException.Message);
            }
        }
    }

    public class TimetampsAspect : Aspect
    {
        public bool UseUtc { get; protected set; }

        public TimetampsAspect()
        {
        }

        /// <summary>
        /// Parameter should be in the format of "useUtc=true;"
        /// </summary>
        /// <param name="configParams"></param>
        public TimetampsAspect(string configParams)
        {
            string useUtcStr = DefaultAspect.GetParameterValue(configParams, "useUtc", "false");
            this.UseUtc = bool.Parse(useUtcStr);
        }

        protected DateTime GetCurrent()
        {
            return this.UseUtc ? DateTime.UtcNow : DateTime.Now;
        }

        public override void Step_2_BeforeTryingMethodExec()
        {
            this.LogInformationData("Timestamp type for Step_2_BeforeTryingMethodExec", this.UseUtc ? "UTC time" : "Local time");
            this.LogInformationData("Timestamp for Step_2_BeforeTryingMethodExec", this.GetCurrent());
        }

        public override void Step_7_AfterEverythingSaidAndDone()
        {
            this.LogInformationData("Timestamp type for Step_7_AfterEverythingSaidAndDone", this.UseUtc ? "UTC time" : "Local time");
            this.LogInformationData("Timestamp for Step_7_AfterEverythingSaidAndDone", this.GetCurrent());
        }
    }
}
