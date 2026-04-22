/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */
namespace NOAutopilot.Core.PID;

public interface IPIDLoop
{
    double PTerm { get; }
    double ITerm { get; }
    double DTerm { get; }
    double Update(double r, double y);
    void Reset();
    void SeedIntegral(double value);
    void SeedOutput(double value);
}
