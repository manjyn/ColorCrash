using UnityEngine;
using ColorCrash.Units;

public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Database Settings")]
    [SerializeField] protected string projectileId;
    public string ProjectileId => projectileId;

    protected float damage;
    protected UnitBase target;
    protected TeamColor teamColor;
    
    public virtual void Initialize(UnitBase target, float damage, TeamColor teamColor)
    {
        this.target = target;
        this.damage = damage;
        this.teamColor = teamColor;
    }
}
