using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpritesheetAnimation
{

  [System.Serializable]
  public struct KeyframeInfo
  {
    [SerializeField]
    public float time;
    [SerializeField]
    public int meshId;
    public void SetMeshId(int tgtId)
    {
      meshId = tgtId;
    }
  }


  [System.Serializable]
  public struct ControllerParameter
  {
    public string name;
    public string type;
  }

  [System.Serializable]
  public struct TransitionCondition
  {
    public string parameter;
    public float threshold;
    public int mode;
  }

  [System.Serializable]
  public struct Transition
  {
    public string destinationState;
    public bool hasExitTime;
    public float exitTime;
    public List<TransitionCondition> conditions;
  }

  [System.Serializable]
  public struct ControllerState
  {
    public string name;
    public string clip;
    public List<Transition> transitions;
    public bool isDefault;

  }

  [System.Serializable]
  public struct KeyframeEvent
  {
    public string name;
    public float time;
  }

  public class Animation
  {
    public string name;
    public List<ControllerParameter> parameters;
    public List<AnimationTrack> tracks;
    public List<ControllerState> states;

    public Animation()
    {
      tracks = new List<AnimationTrack>();
      parameters = new List<ControllerParameter>();
      states = new List<ControllerState>();
    }

  }

  [System.Serializable]
  public class AnimationTrack
  {
    public string name;
    public float frameRate;
    public float duration;
    public bool loop;
    public List<KeyframeInfo> keyframes;
    public List<KeyframeEvent> events;

    public AnimationTrack()
    {
      keyframes = new List<KeyframeInfo>();
      events = new List<KeyframeEvent>();
    }
  }


  [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
  public class SpritesheetAnimationBinding : MonoBehaviour
  {

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    private AnimationClip _currentClip;
    private AnimationTrack _currentAnimation;

    private Sprite _baseSprite;
    private int _frameIdx = 0;
    public int FrameIdx
    {
      get => _frameIdx;
    }

    public void Bind()
    {
      _animator = GetComponent<Animator>();
      _spriteRenderer = GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    [ContextMenu("Test")]
    public void Test()
    {

      Bind();

      var runtimeController = _animator.runtimeAnimatorController;
      if (runtimeController == null)
      {
        Debug.LogErrorFormat("RuntimeAnimatorController must not be null.");
        return;
      }

      var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(runtimeController));
      if (controller == null)
      {
        Debug.LogErrorFormat("AnimatorController must not be null.");
        return;
      }

      var layer = controller.layers[0];
      // Debug.Log("=== PARAMS ===");
      // foreach (var p in controller.parameters)
      // {
      //   Debug.Log(p.name);
      //   Debug.Log(p.type);
      //   Debug.Log("=============");
      // }

      Debug.Log("=============");
      var stateMachine = layer.stateMachine;
      Debug.Log(stateMachine.anyStateTransitions);
      foreach (var state in stateMachine.states)
      {
        // // state.state.motion.name;
        // Debug.Log("==========");
        // Debug.Log("STATE: " + state.state.name);
        // foreach (var transition in state.state.transitions)
        // {
        //   Debug.Log("==========");
        //   Debug.Log(state.state.name + " : To : " + transition.destinationState.name);
        //   foreach (var condition in transition.conditions)
        //   {
        //     Debug.Log(condition.parameter);
        //     Debug.Log(condition.threshold);
        //     Debug.Log(condition.mode);
        //   }
        //   Debug.Log("==========");
        // }
        // Debug.Log("==========");
      }

    }
#endif
    public Animation CreateAnimation()
    {

      var animation = new Animation();
      var runtimeController = _animator.runtimeAnimatorController;

      var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(runtimeController));

      foreach (var p in controller.parameters)
      {
        animation.parameters.Add(new ControllerParameter()
        {
          name = p.name,
          type = p.type.ToString()
        });
      }

      var layer = controller.layers[0];
      var stateMachine = layer.stateMachine;
      var anystateTransitions = stateMachine.anyStateTransitions;

      foreach (var s in stateMachine.states)
      {

        var state = s.state;
        var sourceTransitions = new UnityEditor.Animations.AnimatorStateTransition[anystateTransitions.Length + state.transitions.Length];
        anystateTransitions.CopyTo(sourceTransitions, 0);
        state.transitions.CopyTo(sourceTransitions, anystateTransitions.Length);

        var transitions = new List<Transition>();

        foreach (var transition in sourceTransitions)
        {
          var t = new Transition();
          var dst = transition.destinationState ? transition.destinationState.name : stateMachine.defaultState.name;
          t.destinationState = dst;
          t.hasExitTime = transition.hasExitTime;
          t.exitTime = transition.exitTime;
          t.conditions = new List<TransitionCondition>();
          foreach (var condition in transition.conditions)
          {
            t.conditions.Add(new TransitionCondition()
            {
              mode = (int)condition.mode,
              threshold = condition.threshold,
              parameter = condition.parameter.ToString(),
            });
          }
          transitions.Add(t);
        }


        animation.states.Add(new ControllerState()
        {
          name = state.name,
          clip = state.motion.name,
          transitions = transitions,
          isDefault = stateMachine.defaultState == state
        });
      }

      animation.name = controller.name;

      return animation;

    }

    public string GetCurrentSpriteName()
    {
      return this._spriteRenderer.sprite.name;
    }

    public AnimationTrack Init(AnimationClip clip)
    {

      float duration = clip.averageDuration;
      _frameIdx = 0;
      _baseSprite = _spriteRenderer.sprite;
      var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
      var curve = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);

      var animation = new AnimationTrack();
      animation.name = clip.name;
      animation.frameRate = clip.frameRate;
      animation.duration = clip.averageDuration;
      animation.loop = clip.isLooping;

      for (int i = 0; i < curve.Length; i++)
      {
        animation.keyframes.Add(new KeyframeInfo()
        {
          time = curve[i].time
        }
        );
      }

      _currentAnimation = animation;
      _currentClip = clip;

      var evts = _currentClip.events;
      foreach (var evt in evts)
      {
        animation.events.Add(new KeyframeEvent()
        {
          name = evt.stringParameter,
          time = evt.time
        });
      }

      return animation;

    }

    public void Finish()
    {
      _spriteRenderer.sprite = _baseSprite;
    }

    public string GetControllerName()
    {
      return _animator.runtimeAnimatorController.name;
    }

    public AnimationClip[] GetClips()
    {
      return _animator.runtimeAnimatorController.animationClips;
    }

    public void Sample(int idx)
    {
      _currentClip.SampleAnimation(gameObject, _currentAnimation.keyframes[idx].time);
    }

    public AnimationEvent[] GetEvents()
    {
      return _currentClip.events;
    }

  }

}

