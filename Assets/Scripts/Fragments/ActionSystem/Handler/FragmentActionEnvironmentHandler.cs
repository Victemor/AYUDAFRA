using System.Collections;
using UnityEngine;
using Game.Data;

public sealed class FragmentActionEnvironmentHandler
{
    public IEnumerator Execute(FragmentAction action, FragmentActionExecutionContext context, MonoBehaviour host)
    {
        ActionBehaviorController controller = context.Controller;

        switch (action.ActionType)
        {
            case FragmentActionType.RainStart:
            case FragmentActionType.RainChangeIntensity:
                if (controller.Rain != null)
                {
                    controller.Rain.SetRainEmissionWithTimeTransition(
                        action.RainIntensity,
                        action.RainTransitionTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] RainController no asignado.", host);
                }
                break;

            case FragmentActionType.RainStop:
                if (controller.Rain != null)
                {
                    controller.Rain.StopRain(action.RainTransitionTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] RainController no asignado.", host);
                }
                break;

            case FragmentActionType.WindStart:
            case FragmentActionType.WindChangeIntensity:
                if (controller.Wind != null)
                {
                    controller.Wind.SetWindEmissionWithTransition(
                        action.WindIntensity,
                        action.WindTransitionTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] WindController no asignado.", host);
                }
                break;

            case FragmentActionType.WindStop:
                if (controller.Wind != null)
                {
                    controller.Wind.StopWindWithTransitionTime(action.WindTransitionTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] WindController no asignado.", host);
                }
                break;

            case FragmentActionType.FireStart:
                if (action.TargetFireObject != null)
                {
                    action.TargetFireObject.StartFire(
                        action.FireIntensity,
                        action.FireTransitionInTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] TargetFireObject no asignado.", host);
                }
                break;

            case FragmentActionType.FireStop:
                if (action.TargetFireObject != null)
                {
                    action.TargetFireObject.StopFire(action.FireTransitionOutTime);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] TargetFireObject no asignado.", host);
                }
                break;

            case FragmentActionType.SetWeatherProfile:
                if (controller.EmotionalClimate != null && action.WeatherProfile != null)
                {
                    controller.EmotionalClimate.SetWeather(action.WeatherProfile);
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] EmotionalClimate o WeatherProfile no asignado.", host);
                }
                break;

            case FragmentActionType.ClearWeatherProfile:
                if (controller.EmotionalClimate != null)
                {
                    controller.EmotionalClimate.ClearWeather();
                }
                else
                {
                    Debug.LogWarning("[FragmentActionEnvironmentHandler] EmotionalClimate no asignado.", host);
                }
                break;

            case FragmentActionType.SetBloomIntensity:
            case FragmentActionType.SetBloomTint:
                Debug.LogWarning("[FragmentActionEnvironmentHandler] Acción de Bloom todavía no implementada.", host);
                break;
        }

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        switch (actionType)
        {
            case FragmentActionType.RainStart:
            case FragmentActionType.RainStop:
            case FragmentActionType.RainChangeIntensity:
            case FragmentActionType.WindStart:
            case FragmentActionType.WindStop:
            case FragmentActionType.WindChangeIntensity:
            case FragmentActionType.FireStart:
            case FragmentActionType.FireStop:
            case FragmentActionType.SetBloomIntensity:
            case FragmentActionType.SetBloomTint:
            case FragmentActionType.SetWeatherProfile:
            case FragmentActionType.ClearWeatherProfile:
                return true;

            default:
                return false;
        }
    }
}