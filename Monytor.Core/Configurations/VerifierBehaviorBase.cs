﻿using Monytor.Core.Models;
using Monytor.Core.Repositories;

namespace Monytor.Core.Configurations {
    public abstract class VerifierBehaviorBase : Behavior {
        public ISeriesQueryRepository SeriesRepository { get; set; }
        public abstract VerifyResult Verify(Verifier verifier, Series series);
    }
}
