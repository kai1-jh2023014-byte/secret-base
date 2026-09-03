namespace SecretBase.Core.Connectors;

public static class IntegrationPermissionGate
{
    public static IntegrationPermissionKind RequiredFor(CapabilityRiskKind risk) =>
        risk switch
        {
            CapabilityRiskKind.Read => IntegrationPermissionKind.Read,
            CapabilityRiskKind.Write => IntegrationPermissionKind.Write,
            CapabilityRiskKind.Execute => IntegrationPermissionKind.Execute,
            CapabilityRiskKind.Destructive => IntegrationPermissionKind.Destructive,
            CapabilityRiskKind.External => IntegrationPermissionKind.ExternalCommunication,
            _ => IntegrationPermissionKind.Read
        };

    public static bool Allows(IntegrationPermissionKind granted, CapabilityRiskKind risk)
    {
        var required = RequiredFor(risk);
        if (risk == CapabilityRiskKind.Destructive)
        {
            return granted.HasFlag(IntegrationPermissionKind.Destructive)
                   && granted.HasFlag(IntegrationPermissionKind.Write);
        }

        if (risk == CapabilityRiskKind.External)
        {
            return granted.HasFlag(IntegrationPermissionKind.ExternalCommunication)
                   && granted.HasFlag(IntegrationPermissionKind.Read);
        }

        return granted.HasFlag(required);
    }

    public static bool AllowsBackgroundEvents(IntegrationPermissionKind granted) =>
        granted.HasFlag(IntegrationPermissionKind.BackgroundEvent)
        && granted.HasFlag(IntegrationPermissionKind.Read);

    public static IntegrationPermissionKind DefaultFor(IntegrationManifest manifest)
    {
        var flags = IntegrationPermissionKind.None;
        foreach (var capability in manifest.Capabilities)
        {
            flags |= RequiredFor(capability.Risk);
            if (string.Equals(capability.Id, IntegrationCapabilityIds.EventReceive, StringComparison.OrdinalIgnoreCase))
            {
                flags |= IntegrationPermissionKind.BackgroundEvent;
            }

            if (manifest.Transport is IntegrationTransportKind.Http or IntegrationTransportKind.Https or IntegrationTransportKind.Webhook)
            {
                flags |= IntegrationPermissionKind.ExternalCommunication;
            }
        }

        return flags == IntegrationPermissionKind.None ? IntegrationPermissionKind.Read : flags;
    }

    public static IReadOnlyList<(string Label, IntegrationPermissionKind Flag, bool Granted)> Matrix(
        IntegrationManifest manifest,
        IntegrationPermissionKind granted)
    {
        var rows = new List<(string, IntegrationPermissionKind, bool)>();
        foreach (var capability in manifest.Capabilities)
        {
            var flag = RequiredFor(capability.Risk);
            if (string.Equals(capability.Id, IntegrationCapabilityIds.EventReceive, StringComparison.OrdinalIgnoreCase))
            {
                flag = IntegrationPermissionKind.BackgroundEvent;
            }

            var allowed = string.Equals(capability.Id, IntegrationCapabilityIds.EventReceive, StringComparison.OrdinalIgnoreCase)
                ? AllowsBackgroundEvents(granted)
                : Allows(granted, capability.Risk);
            var label = string.IsNullOrWhiteSpace(capability.DisplayName) ? capability.Id : capability.DisplayName;
            rows.Add((label, flag, allowed));
        }

        return rows;
    }
}

public static class IntegrationSafety
{
    /// <summary>Learning never raises CONFIRM or DESTRUCTIVE to SAFE_AUTO.</summary>
    public const bool MayLearnAutoExecute = false;

    public static IntegrationActionLane Classify(
        IntegrationCapabilityDeclaration capability,
        IntegrationEndpointDeclaration? endpoint)
    {
        if (IntegrationManifestValidator.LooksForbidden(capability.Id)
            || capability.Risk == (CapabilityRiskKind)(-1))
        {
            return IntegrationActionLane.Forbidden;
        }

        if (endpoint is not null
            && string.Equals(endpoint.Method, "DELETE", StringComparison.OrdinalIgnoreCase)
            && capability.Risk != CapabilityRiskKind.Destructive)
        {
            return IntegrationActionLane.Forbidden;
        }

        if (capability.Risk == CapabilityRiskKind.Destructive
            || string.Equals(capability.Id, IntegrationCapabilityIds.DataDelete, StringComparison.OrdinalIgnoreCase))
        {
            return IntegrationActionLane.Destructive;
        }

        if (capability.Risk is CapabilityRiskKind.Execute or CapabilityRiskKind.Write or CapabilityRiskKind.External
            || LooksLikeWrite(capability.Id)
            || (endpoint is not null && !string.Equals(endpoint.Method, "GET", StringComparison.OrdinalIgnoreCase)))
        {
            return IntegrationActionLane.Confirm;
        }

        if (capability.Risk == CapabilityRiskKind.Read)
        {
            return IntegrationActionLane.Read;
        }

        return IntegrationActionLane.Confirm;
    }

    public static bool RequiresConfirmation(IntegrationActionLane lane) =>
        lane is IntegrationActionLane.Confirm or IntegrationActionLane.Destructive;

    public static bool IsForbidden(IntegrationActionLane lane) => lane == IntegrationActionLane.Forbidden;

    /// <summary>Learning may only quiet suggestions — never skip the safety lane.</summary>
    public static IntegrationActionLane AfterLearning(IntegrationActionLane lane) => lane;

    private static bool LooksLikeWrite(string id) =>
        id.EndsWith(".write", StringComparison.OrdinalIgnoreCase)
        || id.EndsWith(".create", StringComparison.OrdinalIgnoreCase)
        || id.EndsWith(".update", StringComparison.OrdinalIgnoreCase)
        || id.EndsWith(".delete", StringComparison.OrdinalIgnoreCase)
        || id.EndsWith(".open", StringComparison.OrdinalIgnoreCase);
}
