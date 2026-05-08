using System;

namespace Change.Framework.Gas
{
    public static class DefaultTargetResolvers
    {
        private static readonly ITargetResolver SelfResolver = new SelfResolverImpl();
        private static readonly ITargetResolver EnemyResolver = new EnemyResolverImpl();
        private static readonly ITargetResolver AllyResolver = new AllyResolverImpl();
        private static readonly ITargetResolver AllEnemiesResolver = new AllEnemiesResolverImpl();
        private static readonly ITargetResolver AllAlliesResolver = new AllAlliesResolverImpl();

        public static ITargetResolver Create(TargetType targetType)
        {
            return targetType switch
            {
                TargetType.Self => SelfResolver,
                TargetType.Enemy => EnemyResolver,
                TargetType.Ally => AllyResolver,
                TargetType.AllEnemies => AllEnemiesResolver,
                TargetType.AllAllies => AllAlliesResolver,
                _ => throw new InvalidOperationException($"Unsupported target type: {targetType}")
            };
        }

        private sealed class SelfResolverImpl : ITargetResolver
        {
            public TargetType Type => TargetType.Self;

            public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
            {
                return new[] { source };
            }
        }

        private sealed class EnemyResolverImpl : ITargetResolver
        {
            public TargetType Type => TargetType.Enemy;

            public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
            {
                for (var i = 0; i < allEntities.Length; i++)
                {
                    var candidate = allEntities[i];
                    if (candidate.TeamId != source.TeamId)
                    {
                        return new[] { candidate };
                    }
                }

                return Array.Empty<IAbilitySystem>();
            }
        }

        private sealed class AllyResolverImpl : ITargetResolver
        {
            public TargetType Type => TargetType.Ally;

            public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
            {
                for (var i = 0; i < allEntities.Length; i++)
                {
                    var candidate = allEntities[i];
                    if (candidate.TeamId == source.TeamId && !ReferenceEquals(candidate, source))
                    {
                        return new[] { candidate };
                    }
                }

                return Array.Empty<IAbilitySystem>();
            }
        }

        private sealed class AllEnemiesResolverImpl : ITargetResolver
        {
            public TargetType Type => TargetType.AllEnemies;

            public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
            {
                var count = 0;
                for (var i = 0; i < allEntities.Length; i++)
                {
                    if (allEntities[i].TeamId != source.TeamId)
                    {
                        count++;
                    }
                }

                if (count == 0)
                {
                    return Array.Empty<IAbilitySystem>();
                }

                var result = new IAbilitySystem[count];
                var index = 0;
                for (var i = 0; i < allEntities.Length; i++)
                {
                    var candidate = allEntities[i];
                    if (candidate.TeamId != source.TeamId)
                    {
                        result[index++] = candidate;
                    }
                }

                return result;
            }
        }

        private sealed class AllAlliesResolverImpl : ITargetResolver
        {
            public TargetType Type => TargetType.AllAllies;

            public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
            {
                var count = 0;
                for (var i = 0; i < allEntities.Length; i++)
                {
                    var candidate = allEntities[i];
                    if (candidate.TeamId == source.TeamId && !ReferenceEquals(candidate, source))
                    {
                        count++;
                    }
                }

                if (count == 0)
                {
                    return Array.Empty<IAbilitySystem>();
                }

                var result = new IAbilitySystem[count];
                var index = 0;
                for (var i = 0; i < allEntities.Length; i++)
                {
                    var candidate = allEntities[i];
                    if (candidate.TeamId == source.TeamId && !ReferenceEquals(candidate, source))
                    {
                        result[index++] = candidate;
                    }
                }

                return result;
            }
        }
    }
}
