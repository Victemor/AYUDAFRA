using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionSpriteHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        VisualObjectSpriteController spriteObject = action.TargetSpriteObject;

        if (spriteObject == null)
        {
            Debug.LogWarning("[FragmentActionSpriteHandler] TargetSpriteObject no asignado.", host);
            yield break;
        }

        switch (action.ActionType)
        {
            case FragmentActionType.SpriteFadeIn:
                spriteObject.FadeIn(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteFadeOut:
                spriteObject.FadeOut(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteDissolve:
                spriteObject.Dissolve(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteDissolveVertical:
                spriteObject.DissolveVertical(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteDissolveBoth:
                spriteObject.DissolveBoth(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteAppearDissolve:
                spriteObject.AppearDisolve(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteAppearDissolveVertical:
                spriteObject.AppearDisolveVertical(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteAppearDissolveBoth:
                spriteObject.AppearDisolveBoth(action.SpriteTransitionTime);
                break;

            case FragmentActionType.SpriteFadeMaterialColor:
                spriteObject.FadeMaterialColor(
                    action.EmissiveColor,
                    action.SpriteTransitionTime,
                    action.FadeMaterialParticles);
                break;
        }

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        switch (actionType)
        {
            case FragmentActionType.SpriteFadeIn:
            case FragmentActionType.SpriteFadeOut:
            case FragmentActionType.SpriteDissolve:
            case FragmentActionType.SpriteDissolveVertical:
            case FragmentActionType.SpriteDissolveBoth:
            case FragmentActionType.SpriteAppearDissolve:
            case FragmentActionType.SpriteAppearDissolveVertical:
            case FragmentActionType.SpriteAppearDissolveBoth:
            case FragmentActionType.SpriteFadeMaterialColor:
                return true;

            default:
                return false;
        }
    }
}