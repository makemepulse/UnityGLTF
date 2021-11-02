#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF.Extensions;

namespace UnityGLTF
{

	public partial class GLTFSceneExporter
	{

		public enum ROTATION_TYPE
		{
			UNKNOWN,
			QUATERNION,
			EULER
		};

		static int bakingFramerate = 30; // FPS
		static bool bake = true;

		private struct TargetCurveSet
		{
			public AnimationCurve[] translationCurves;
			public AnimationCurve[] rotationCurves;
			//Additional curve types
			public AnimationCurve[] localEulerAnglesRaw;
			public AnimationCurve[] m_LocalEuler;
			public AnimationCurve[] scaleCurves;
			public ROTATION_TYPE rotationType;
			public void Init()
			{
				translationCurves = new AnimationCurve[3];
				rotationCurves = new AnimationCurve[4];
				scaleCurves = new AnimationCurve[3];
			}
		}


		private void ExportAnimation()
		{

			for (int i = 0; i < _animatedNodes.Count; ++i)
			{
				Transform t = _animatedNodes[i];
				Debug.Log("AnimatedNode " + t.name);
				CreateAndExportAnimationFromNode(ref t);
				// GLTFAnimation anim = new GLTFAnimation();
				// anim.Name = t.name;
				// ExportAnimationFromNode(ref t, ref anim);
				// if (anim.Channels.Count > 0 && anim.Samplers.Count > 0)
				// {
				//     _root.Animations.Add(anim);
				// }
			}

		}


		public void CreateAndExportAnimationFromNode(ref Transform transform)
		{
#if UNITY_EDITOR


			UnityEngine.Animation animation = transform.GetComponent<UnityEngine.Animation>();

			if (animation != null)
			{
				AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
				for (int i = 0; i < clips.Length; i++)
				{
					GLTFAnimation anim = new GLTFAnimation();
					anim.Name = clips[i].name;
					//FIXME It seems not good to generate one animation per animator.
					ConvertClipToGLTFAnimation(clips[i], transform, anim);
					if (anim.Channels.Count > 0 && anim.Samplers.Count > 0)
					{
						Debug.Log(anim);
						_root.Animations.Add(anim);
					}
				}
			}


			Animator a = transform.GetComponent<Animator>();
			if (a != null)
			{
				AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
				if (a.avatar != null)
				{
					// a.avatar
					for (int i = 0; i < clips.Length; i++)
					{
						GLTFAnimation anim = new GLTFAnimation();
						anim.Name = clips[i].name;
						//FIXME It seems not good to generate one animation per animator.
						ConvertClipToGLTFAnimation(clips[i], transform, anim, a);
						if (anim.Channels.Count > 0 && anim.Samplers.Count > 0)
						{
							_root.Animations.Add(anim);
						}
					}
				}
				else
				{
					for (int i = 0; i < clips.Length; i++)
					{
						GLTFAnimation anim = new GLTFAnimation();
						anim.Name = clips[i].name;
						//FIXME It seems not good to generate one animation per animator.
						ConvertClipToGLTFAnimation(clips[i], transform, anim);
						if (anim.Channels.Count > 0 && anim.Samplers.Count > 0)
						{
							_root.Animations.Add(anim);
						}
					}
				}
			}
#endif
		}


		// Parses Animation/Animator component and generate a glTF animation for the active clip
		public void ExportAnimationFromNode(ref Transform transform, ref GLTFAnimation anim)
		{
#if UNITY_EDITOR
			Animator a = transform.GetComponent<Animator>();
			if (a != null)
			{
				AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
				if (a.avatar != null)
				{
					// a.avatar
					for (int i = 0; i < clips.Length; i++)
					{
						//FIXME It seems not good to generate one animation per animator.
						ConvertClipToGLTFAnimation(clips[i], transform, anim, a);
					}
				}
				else
				{
					for (int i = 0; i < clips.Length; i++)
					{
						//FIXME It seems not good to generate one animation per animator.
						ConvertClipToGLTFAnimation(clips[i], transform, anim);
					}
				}
			}

			UnityEngine.Animation animation = transform.GetComponent<UnityEngine.Animation>();
			if (animation != null)
			{
				AnimationClip[] clips = AnimationUtility.GetAnimationClips(transform.gameObject);
				for (int i = 0; i < clips.Length; i++)
				{
					//FIXME It seems not good to generate one animation per animator.
					ConvertClipToGLTFAnimation(clips[i], transform, anim);
				}
			}
#endif
		}


		private int GetTargetIdFromTransform(ref Transform transform)
		{
			if (_nodesByInstanceId.ContainsKey(transform.GetInstanceID()))
			{
				return _nodesByInstanceId[transform.GetInstanceID()].Id;
			}
			else
			{
				Debug.LogWarning(transform.name + " " + transform.GetInstanceID());
				return 0;
			}
		}

		private List<Transform> CollectAvatarTransforms(Animator animator)
		{
			var res = new List<Transform>();
			if (animator.avatar.isHuman)
			{
				for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
				{
					var tr = animator.GetBoneTransform((HumanBodyBones)i);
					if (tr != null) res.Add(tr);
				}
			}
			else
			{
				Transform rigRoot = null;
				int tcount = animator.transform.childCount;
				for (int i = 0; i < tcount; i++)
				{
					if (!animator.transform.GetChild(i).GetComponent<SkinnedMeshRenderer>())
					{
						rigRoot = animator.transform.GetChild(i);
					}
				}
				if (rigRoot != null)
				{
					var ts = rigRoot.GetComponentsInChildren<Transform>();
					for (int i = 0; i < ts.Length; i++)
					{
						res.Add(ts[i]);
					}
				}
			}
			return res;
		}

		private void ConvertClipToGLTFAnimation(AnimationClip clip, Transform transform, GLTFAnimation animation, Animator animator)
		{
			Debug.Log($"ConvertClipToGLTFAnimation clip.length : {clip.length}");
			// Debug.Log(transform.name);
			AnimationCurve[] rootMotionCurves = new AnimationCurve[3];
			if (clip.hasRootCurves)
			{

				foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
				{
					if (binding.propertyName == "MotionT.x")
					{
						rootMotionCurves[0] = AnimationUtility.GetEditorCurve(clip, binding);
						var curve = rootMotionCurves[0];
						var duration = curve.keys[curve.length - 1].time;
						Debug.Log($"ConvertClipToGLTFAnimation rootmotion curve duration: {duration}");
					}
					else if (binding.propertyName == "MotionT.y")
					{
						rootMotionCurves[1] = AnimationUtility.GetEditorCurve(clip, binding);
					}
					else if (binding.propertyName == "MotionT.z")
					{
						rootMotionCurves[2] = AnimationUtility.GetEditorCurve(clip, binding);
					}
				}
			}


			var allBones = CollectAvatarTransforms(animator);
			int nbSamples = (int)(clip.length * bakingFramerate);
			float deltaTime = clip.length / nbSamples;

			float[] times = new float[nbSamples];
			for (int t = 0; t < nbSamples; ++t)
			{
				float currentTime = t * deltaTime;
				times[t] = currentTime;
			}

			for (int i = 0; i < allBones.Count; i++)
			{
				Transform bone = allBones[i].transform;

				// if( !animator.applyRootMotion && bone == animator)
				// Initialize Arrays
				var positions = new Vector3[nbSamples];
				var rotations = new Vector4[nbSamples];
				var scales = new Vector3[nbSamples];



				for (int j = 0; j < nbSamples; ++j)
				{
					float currentTime = j * deltaTime;
					clip.SampleAnimation(transform.gameObject, currentTime);
					positions[j] = bone.localPosition;
					rotations[j] = new Vector4(bone.localRotation.x, bone.localRotation.y, bone.localRotation.z, bone.localRotation.w);
					scales[j] = bone.localScale;
					// Debug.Log(allBones[i].transform.name + " " + positions[j]);
				}


				int channelTargetId = GetTargetIdFromTransform(ref bone);
				AccessorId timeAccessor = ExportAccessor(times);

				// cancel root motion from root motion  curve
				if (!animator.applyRootMotion && i == 0)
				{

					if (rootMotionCurves[0] != null)
					{

						for (int j = 0; j < nbSamples; ++j)
						{
							float currentTime = j * deltaTime;
							positions[j].x -= rootMotionCurves[0].Evaluate(currentTime);
							positions[j].y -= rootMotionCurves[1].Evaluate(currentTime);
							positions[j].z -= rootMotionCurves[2].Evaluate(currentTime);
						}

					}
				}

				// assume Hips is the root bone
				// skip it's position animation if controller don't apply root motion

				// Create channel
				AnimationChannel Tchannel = new AnimationChannel();
				AnimationChannelTarget TchannelTarget = new AnimationChannelTarget();
				TchannelTarget.Path = GLTFAnimationChannelPath.translation;
				TchannelTarget.Node = new NodeId
				{
					Id = channelTargetId,
					Root = _root
				};

				Tchannel.Target = TchannelTarget;

				AnimationSampler Tsampler = new AnimationSampler();
				Tsampler.Input = timeAccessor;
				Tsampler.Output = ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(positions, SchemaExtensions.CoordinateSpaceConversionScale)); // Sketchfab change handedness here (-z)
				Tchannel.Sampler = new AnimationSamplerId
				{
					Id = animation.Samplers.Count,
					GLTFAnimation = animation,
					Root = _root
				};

				animation.Samplers.Add(Tsampler);
				animation.Channels.Add(Tchannel);
				// }

				// Rotation
				AnimationChannel Rchannel = new AnimationChannel();
				AnimationChannelTarget RchannelTarget = new AnimationChannelTarget();
				RchannelTarget.Path = GLTFAnimationChannelPath.rotation;
				RchannelTarget.Node = new NodeId
				{
					Id = channelTargetId,
					Root = _root
				};

				Rchannel.Target = RchannelTarget;

				AnimationSampler Rsampler = new AnimationSampler();
				Rsampler.Input = timeAccessor; // Float, for time
				Rsampler.Output = ExportAccessor(SchemaExtensions.ConvertQuaternionsCoordinateSpaceAndCopy(rotations)); // Vec4 for
				Rchannel.Sampler = new AnimationSamplerId
				{
					Id = animation.Samplers.Count,
					GLTFAnimation = animation,
					Root = _root
				};

				animation.Samplers.Add(Rsampler);
				animation.Channels.Add(Rchannel);

				// Scale
				AnimationChannel Schannel = new AnimationChannel();
				AnimationChannelTarget SchannelTarget = new AnimationChannelTarget();
				SchannelTarget.Path = GLTFAnimationChannelPath.scale;
				SchannelTarget.Node = new NodeId
				{
					Id = channelTargetId,
					Root = _root
				};

				Schannel.Target = SchannelTarget;

				AnimationSampler Ssampler = new AnimationSampler();
				Ssampler.Input = timeAccessor; // Float, for time
				Ssampler.Output = ExportAccessor(scales); // Vec3 for scale
				Schannel.Sampler = new AnimationSamplerId
				{
					Id = animation.Samplers.Count,
					GLTFAnimation = animation,
					Root = _root
				};

				animation.Samplers.Add(Ssampler);
				animation.Channels.Add(Schannel);


			}
		}


		private void ConvertClipToGLTFAnimation(AnimationClip clip, Transform transform, GLTFAnimation animation)
		{
			// Generate GLTF.Schema.AnimationChannel and GLTF.Schema.AnimationSampler
			// 1 channel per node T/R/S, one sampler per node T/R/S
			// Need to keep a list of nodes to convert to indexes

			// 1. browse clip, collect all curves and create a TargetCurveSet for each target
			Dictionary<string, TargetCurveSet> targetCurvesBinding = new Dictionary<string, TargetCurveSet>();
			CollectClipCurves(clip, ref targetCurvesBinding);

			// Baking needs all properties, fill missing curves with transform data in 2 keyframes (start, endTime)
			// where endTime is clip duration
			// Note: we should avoid creating curves for a property if none of it's components is animated
			GenerateMissingCurves(clip.length, ref transform, ref targetCurvesBinding);

			if (bake)
			{
				// Bake animation for all animated nodes
				foreach (string target in targetCurvesBinding.Keys)
				{
					Debug.Log("target : " + target);
					Transform targetTr = target.Length > 0 ? transform.Find(target) : transform;
					if (targetTr == null || targetTr.GetComponent<SkinnedMeshRenderer>())
					{
						continue;
					}


					// Initialize data
					// Bake and populate animation data
					float[] times = null;
					Vector3[] positions = null;
					Vector3[] scales = null;
					Vector4[] rotations = null;
					BakeCurveSet(targetCurvesBinding[target], clip.length, bakingFramerate, ref times, ref positions, ref rotations, ref scales);

					int channelTargetId = GetTargetIdFromTransform(ref targetTr);
					AccessorId timeAccessor = ExportAccessor(times);

					// Create channel
					AnimationChannel Tchannel = new AnimationChannel();
					AnimationChannelTarget TchannelTarget = new AnimationChannelTarget();
					TchannelTarget.Path = GLTFAnimationChannelPath.translation;
					TchannelTarget.Node = new NodeId
					{
						Id = channelTargetId,
						Root = _root
					};

					Tchannel.Target = TchannelTarget;

					AnimationSampler Tsampler = new AnimationSampler();
					Tsampler.Input = timeAccessor;
					Tsampler.Output = ExportAccessor(SchemaExtensions.ConvertVector3CoordinateSpaceAndCopy(positions, SchemaExtensions.CoordinateSpaceConversionScale)); // Sketchfab change handedness here (-z)
					Tchannel.Sampler = new AnimationSamplerId
					{
						Id = animation.Samplers.Count,
						GLTFAnimation = animation,
						Root = _root
					};

					animation.Samplers.Add(Tsampler);
					animation.Channels.Add(Tchannel);

					// Rotation
					AnimationChannel Rchannel = new AnimationChannel();
					AnimationChannelTarget RchannelTarget = new AnimationChannelTarget();
					RchannelTarget.Path = GLTFAnimationChannelPath.rotation;
					RchannelTarget.Node = new NodeId
					{
						Id = channelTargetId,
						Root = _root
					};

					Rchannel.Target = RchannelTarget;

					AnimationSampler Rsampler = new AnimationSampler();
					Rsampler.Input = timeAccessor; // Float, for time
					Rsampler.Output = ExportAccessor(SchemaExtensions.ConvertQuaternionsCoordinateSpaceAndCopy(rotations)); // Vec4 for
					Rchannel.Sampler = new AnimationSamplerId
					{
						Id = animation.Samplers.Count,
						GLTFAnimation = animation,
						Root = _root
					};

					animation.Samplers.Add(Rsampler);
					animation.Channels.Add(Rchannel);

					// Scale
					AnimationChannel Schannel = new AnimationChannel();
					AnimationChannelTarget SchannelTarget = new AnimationChannelTarget();
					SchannelTarget.Path = GLTFAnimationChannelPath.scale;
					SchannelTarget.Node = new NodeId
					{
						Id = channelTargetId,
						Root = _root
					};

					Schannel.Target = SchannelTarget;

					AnimationSampler Ssampler = new AnimationSampler();
					Ssampler.Input = timeAccessor; // Float, for time
					Ssampler.Output = ExportAccessor(scales); // Vec3 for scale
					Schannel.Sampler = new AnimationSamplerId
					{
						Id = animation.Samplers.Count,
						GLTFAnimation = animation,
						Root = _root
					};

					animation.Samplers.Add(Ssampler);
					animation.Channels.Add(Schannel);
				}
			}
			else
			{
				Debug.LogError("Only baked animation is supported for now. Skipping animation");
			}

		}

		private void CollectClipCurves(AnimationClip clip, ref Dictionary<string, TargetCurveSet> targetCurves)
		{
			foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
			{
				AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

				Debug.Log(binding.propertyName);
				if (!targetCurves.ContainsKey(binding.path))
				{
					TargetCurveSet curveSet = new TargetCurveSet();
					curveSet.Init();
					targetCurves.Add(binding.path, curveSet);
				}

				TargetCurveSet current = targetCurves[binding.path];
				if (binding.propertyName.Contains("m_LocalPosition"))
				{
					if (binding.propertyName.Contains(".x"))
						current.translationCurves[0] = curve;
					else if (binding.propertyName.Contains(".y"))
						current.translationCurves[1] = curve;
					else if (binding.propertyName.Contains(".z"))
						current.translationCurves[2] = curve;
				}
				else if (binding.propertyName.Contains("m_LocalScale"))
				{
					if (binding.propertyName.Contains(".x"))
						current.scaleCurves[0] = curve;
					else if (binding.propertyName.Contains(".y"))
						current.scaleCurves[1] = curve;
					else if (binding.propertyName.Contains(".z"))
						current.scaleCurves[2] = curve;
				}
				else if (binding.propertyName.ToLower().Contains("localrotation"))
				{
					current.rotationType = ROTATION_TYPE.QUATERNION;
					if (binding.propertyName.Contains(".x"))
						current.rotationCurves[0] = curve;
					else if (binding.propertyName.Contains(".y"))
						current.rotationCurves[1] = curve;
					else if (binding.propertyName.Contains(".z"))
						current.rotationCurves[2] = curve;
					else if (binding.propertyName.Contains(".w"))
						current.rotationCurves[3] = curve;
				}
				// Takes into account 'localEuler', 'localEulerAnglesBaked' and 'localEulerAnglesRaw'
				else if (binding.propertyName.ToLower().Contains("localeuler"))
				{
					current.rotationType = ROTATION_TYPE.EULER;
					if (binding.propertyName.Contains(".x"))
						current.rotationCurves[0] = curve;
					else if (binding.propertyName.Contains(".y"))
						current.rotationCurves[1] = curve;
					else if (binding.propertyName.Contains(".z"))
						current.rotationCurves[2] = curve;
				}
				targetCurves[binding.path] = current;
			}
		}

		private void GenerateMissingCurves(float endTime, ref Transform tr, ref Dictionary<string, TargetCurveSet> targetCurvesBinding)
		{
			foreach (string target in targetCurvesBinding.Keys)
			{
				Transform targetTr = target.Length > 0 ? tr.Find(target) : tr;
				if (targetTr == null)
					continue;

				TargetCurveSet current = targetCurvesBinding[target];
				if (current.translationCurves[0] == null)
				{
					current.translationCurves[0] = CreateConstantCurve(targetTr.localPosition.x, endTime);
					current.translationCurves[1] = CreateConstantCurve(targetTr.localPosition.y, endTime);
					current.translationCurves[2] = CreateConstantCurve(targetTr.localPosition.z, endTime);
				}

				if (current.scaleCurves[0] == null)
				{
					current.scaleCurves[0] = CreateConstantCurve(targetTr.localScale.x, endTime);
					current.scaleCurves[1] = CreateConstantCurve(targetTr.localScale.y, endTime);
					current.scaleCurves[2] = CreateConstantCurve(targetTr.localScale.z, endTime);
				}

				if (current.rotationCurves[0] == null)
				{
					current.rotationCurves[0] = CreateConstantCurve(targetTr.localRotation.x, endTime);
					current.rotationCurves[1] = CreateConstantCurve(targetTr.localRotation.y, endTime);
					current.rotationCurves[2] = CreateConstantCurve(targetTr.localRotation.z, endTime);
					current.rotationCurves[3] = CreateConstantCurve(targetTr.localRotation.w, endTime);
				}
			}
		}

		private AnimationCurve CreateConstantCurve(float value, float endTime)
		{
			// No translation curves, adding them
			AnimationCurve curve = new AnimationCurve();
			curve.AddKey(0, value);
			curve.AddKey(endTime, value);
			return curve;
		}

		private void BakeCurveSet(TargetCurveSet curveSet, float length, int bakingFramerate, ref float[] times, ref Vector3[] positions, ref Vector4[] rotations, ref Vector3[] scales)
		{
			int nbSamples = (int)(length * 30);
			float deltaTime = length / nbSamples;

			// Initialize Arrays
			times = new float[nbSamples];
			positions = new Vector3[nbSamples];
			scales = new Vector3[nbSamples];
			rotations = new Vector4[nbSamples];

			// Assuming all the curves exist now
			for (int i = 0; i < nbSamples; ++i)
			{
				float currentTime = i * deltaTime;
				times[i] = currentTime;
				positions[i] = new Vector3(curveSet.translationCurves[0].Evaluate(currentTime), curveSet.translationCurves[1].Evaluate(currentTime), curveSet.translationCurves[2].Evaluate(currentTime));
				scales[i] = new Vector3(curveSet.scaleCurves[0].Evaluate(currentTime), curveSet.scaleCurves[1].Evaluate(currentTime), curveSet.scaleCurves[2].Evaluate(currentTime));
				if (curveSet.rotationType == ROTATION_TYPE.EULER)
				{
					Quaternion eulerToQuat = Quaternion.Euler(curveSet.rotationCurves[0].Evaluate(currentTime), curveSet.rotationCurves[1].Evaluate(currentTime), curveSet.rotationCurves[2].Evaluate(currentTime));
					rotations[i] = new Vector4(eulerToQuat.x, eulerToQuat.y, eulerToQuat.z, eulerToQuat.w);
				}
				else
				{
					rotations[i] = new Vector4(curveSet.rotationCurves[0].Evaluate(currentTime), curveSet.rotationCurves[1].Evaluate(currentTime), curveSet.rotationCurves[2].Evaluate(currentTime), curveSet.rotationCurves[3].Evaluate(currentTime));
				}
			}
		}


	}
}