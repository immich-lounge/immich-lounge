using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmichLoungeCompanion.Models;

public static class ProfileAssetFilterExtensions
{
    public static void NormalizeAssetFilter(this Profile profile)
    {
        profile.AssetFilter ??= BuildAssetFilterFromContentSources(profile.ContentSources);
    }

    public static bool HasMemoriesSource(this Profile profile) =>
        profile.ContentSources.Any(source => string.Equals(source.Type, "memories", StringComparison.OrdinalIgnoreCase));

    public static AssetFilterRule? BuildAssetFilterFromContentSources(IEnumerable<ContentSource> contentSources)
    {
        var children = contentSources
            .Where(source => !string.Equals(source.Type, "memories", StringComparison.OrdinalIgnoreCase))
            .Select(ToConditionRule)
            .Where(rule => rule != null)
            .Cast<AssetFilterRule>()
            .ToList();

        return children.Count == 0
            ? null
            : new AssetFilterRule
            {
                Kind = AssetFilterRuleKind.Group,
                Operator = AssetFilterGroupOperator.Or,
                Children = children
            };
    }

    public static string? ValidateAssetFilter(this Profile profile)
    {
        if (profile.AssetFilter == null)
        {
            return null;
        }

        return ValidateNode(profile.AssetFilter);
    }

    private static string? ValidateNode(AssetFilterRule rule)
    {
        return rule.Kind switch
        {
            AssetFilterRuleKind.Group => ValidateGroup(rule),
            AssetFilterRuleKind.Condition => ValidateCondition(rule),
            _ => "Invalid asset filter rule kind."
        };
    }

    private static string? ValidateGroup(AssetFilterRule rule)
    {
        if (rule.Operator == null)
        {
            return "Asset filter group rules must specify an operator.";
        }

        if (rule.Type != null || !string.IsNullOrWhiteSpace(rule.Id) || !string.IsNullOrWhiteSpace(rule.Label))
        {
            return "Asset filter group rules cannot specify condition fields.";
        }

        if (rule.Children.Count == 0)
        {
            return "Asset filter group rules must contain at least one child.";
        }

        foreach (var child in rule.Children)
        {
            var error = ValidateNode(child);
            if (error != null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateCondition(AssetFilterRule rule)
    {
        if (rule.Type == null)
        {
            return "Asset filter condition rules must specify a type.";
        }

        if (rule.Operator != null || rule.Children.Count > 0)
        {
            return "Asset filter condition rules cannot contain group fields.";
        }

        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            return "Asset filter condition rules must specify an id.";
        }

        return null;
    }

    private static AssetFilterRule? ToConditionRule(ContentSource source)
    {
        if (!TryMapSourceType(source.Type, out var conditionType))
        {
            return null;
        }

        return new AssetFilterRule
        {
            Kind = AssetFilterRuleKind.Condition,
            Type = conditionType,
            Id = source.Id,
            Label = source.Label
        };
    }

    private static bool TryMapSourceType(string sourceType, out AssetFilterConditionType conditionType)
    {
        switch (sourceType)
        {
            case "album":
                conditionType = AssetFilterConditionType.Album;
                return true;
            case "person":
                conditionType = AssetFilterConditionType.Person;
                return true;
            case "tag":
                conditionType = AssetFilterConditionType.Tag;
                return true;
            default:
                conditionType = default;
                return false;
        }
    }
}
