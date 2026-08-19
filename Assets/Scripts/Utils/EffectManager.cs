using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System;

namespace ColorCrash.Effects
{
    [System.Serializable]
    public class EffectPrefabMapping
    {
        public EffectType type;
        public EffectController prefab;
        public int defaultCapacity = 20;
        public int maxSize = 100;
    }

    /// <summary>
    /// 전역 이펙트 생성을 담당하는 매니저 클래스.
    /// UnityEngine.Pool.ObjectPool을 사용하여 최적화된 파티클 생성을 지원합니다.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        [Header("Effect Settings")]
        [SerializeField] private List<EffectPrefabMapping> effectMappings = new List<EffectPrefabMapping>();

        // 이펙트 타입별 풀 저장소
        private Dictionary<EffectType, ObjectPool<EffectController>> pools = new Dictionary<EffectType, ObjectPool<EffectController>>();
        private Dictionary<EffectType, EffectController> prefabLookup = new Dictionary<EffectType, EffectController>();
        
        private Transform poolContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            poolContainer = new GameObject("EffectPool").transform;
            poolContainer.SetParent(transform);

            InitializePools();
        }

        private void InitializePools()
        {
            if (effectMappings.Find(m => m.type == EffectType.TileChange) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.TileChange, prefab = CreateTileChangeVfx(), defaultCapacity = 20, maxSize = 100 });
            
            if (effectMappings.Find(m => m.type == EffectType.MeleeSlash) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.MeleeSlash, prefab = CreateFootmanAttackVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.CannonFire) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.CannonFire, prefab = CreateCannonFireVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.CannonExplosion) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.CannonExplosion, prefab = CreateCannonExplosionVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.MortarExplosion) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.MortarExplosion, prefab = CreateMortarExplosionVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.UnitDestroy) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.UnitDestroy, prefab = CreateUnitDeathVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.TowerDestroy) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.TowerDestroy, prefab = CreateTowerDestoryVfx(), defaultCapacity = 20, maxSize = 100 });

            if (effectMappings.Find(m => m.type == EffectType.InkBombExplosion) == null)
                effectMappings.Add(new EffectPrefabMapping { type = EffectType.InkBombExplosion, prefab = CreateInkBombExplosionVfx(), defaultCapacity = 10, maxSize = 50 });

            foreach (var mapping in effectMappings)
            {
                if (mapping.prefab == null) continue;

                prefabLookup[mapping.type] = mapping.prefab;

                pools[mapping.type] = new ObjectPool<EffectController>(
                    createFunc: () =>
                    {
                        var instance = Instantiate(prefabLookup[mapping.type], poolContainer);
                        instance.Initialize(mapping.type, OnEffectFinished);
                        return instance;
                    },
                    actionOnGet: (obj) =>
                    {
                        // Transform 갱신 버그를 막기 위해 여기서 켜지 않고 PlayEffect에서 위치 이동 후 켭니다.
                    },
                    actionOnRelease: (obj) =>
                    {
                        obj.gameObject.SetActive(false);
                    },
                    actionOnDestroy: (obj) =>
                    {
                        Destroy(obj.gameObject);
                    },
                    collectionCheck: false,
                    defaultCapacity: mapping.defaultCapacity,
                    maxSize: mapping.maxSize
                );
            }
        }

        /// <summary>
        /// 타일 진영(컬러) 변경 이펙트
        /// </summary>
        private EffectController CreateTileChangeVfx()
        {
            GameObject go = new GameObject($"PF_Effect_TileChange");
            go.SetActive(false);
            go.transform.SetParent(transform);
            go.transform.rotation = Quaternion.identity;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f; // 제자리에서 번쩍임
            main.startSize = 12f; // 타일 크기
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.loop = false; // 파티클 반복 방지
            main.stopAction = ParticleSystemStopAction.Callback;

            var emission = ps.emission;
            emission.rateOverTime = 0;            
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) }); // 1개의 입자만 크게 띄워서 번쩍이는 효과

            var shape = ps.shape;
            shape.enabled = false;

            // 카메라와 무관하게 무조건 위쪽(Y축)을 바라보며 바닥에 깔리는 모드 설정
            var renderer = ps.GetComponent<ParticleSystemRenderer>();            
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            // Additive(가산 혼합) 쉐이더로 빛나는 느낌 강조
            Material mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (mat.shader == null) mat = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material = mat;

            // 크기가 순간적으로 커지는 애니메이션
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.2f, 1.2f),
                new Keyframe(1f, 1.5f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // 빠르게 나타났다가 서서히 사라지는 알파 페이드 아웃
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.2f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// Footman 유닛이 공격시 타격 이펙트 (검기 궤적)
        /// </summary>        
        private EffectController CreateFootmanAttackVfx()
        {
            GameObject go = new GameObject($"PF_Effect_FootmanAttack");
            go.SetActive(false);
            go.transform.SetParent(transform);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.15f;
            main.startLifetime = 0.15f; // 매우 짧은 생명주기로 휙 사라짐
            main.startSpeed = 15f; // 빠르게 날아감
            main.startSize = 3f; // 굵기
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Callback;

            // 부채꼴(원뿔) 형태로 발사
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f; // 부채꼴 각도
            shape.radius = 0.1f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            // 단발로 여러 가닥의 선을 동시에 쏨
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 2, 5) });

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            // 이동 방향으로 길게 늘어나는 Stretched Billboard 사용하여 칼자국 표현
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f; 
            renderer.velocityScale = 0.2f; // 속도 비례 늘어짐

            Material mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (mat.shader == null) mat = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material = mat;

            // 점점 얇아지는 (칼끝이 뾰족해지는) 애니메이션
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // 빠르게 투명해지는 애니메이션
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 대포 발사 시 섬광과 포연 이펙트 (부모-자식 구조)
        /// </summary>
        private EffectController CreateCannonFireVfx()
        {
            // 1. 부모 오브젝트 (섬광 - Muzzle Flash)
            GameObject go = new GameObject($"PF_Effect_CannonFire");
            go.SetActive(false);
            go.transform.SetParent(transform);

            ParticleSystem flashPs = go.AddComponent<ParticleSystem>();
            var flashMain = flashPs.main;
            flashMain.duration = 0.1f;
            flashMain.startLifetime = 0.1f;
            flashMain.startSpeed = 10f;
            flashMain.startSize = 5f;
            flashMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            flashMain.playOnAwake = false;
            flashMain.loop = false;
            flashMain.stopAction = ParticleSystemStopAction.Callback;

            var flashShape = flashPs.shape;
            flashShape.shapeType = ParticleSystemShapeType.Cone;
            flashShape.angle = 25f;
            flashShape.radius = 0.2f;

            var flashEmission = flashPs.emission;
            flashEmission.rateOverTime = 0;
            flashEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 2, 4) });

            var flashRenderer = flashPs.GetComponent<ParticleSystemRenderer>();
            flashRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            flashRenderer.lengthScale = 1.5f;

            Material addMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (addMat.shader == null) addMat = new Material(Shader.Find("Particles/Standard Unlit"));
            flashRenderer.material = addMat;

            // 섬광의 투명도를 부드럽게 조절 (처음에 약간 투명하게 시작해서 끝에 투명해지게 설정)
            var flashColorCurve = flashPs.colorOverLifetime;
            flashColorCurve.enabled = true;
            Gradient flashGrad = new Gradient();
            flashGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) } // 시작 알파 0.7, 끝 알파 0
            );
            flashColorCurve.color = new ParticleSystem.MinMaxGradient(flashGrad);

            // 2. 자식 오브젝트 (포연 - Smoke)
            GameObject smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(go.transform);
            smokeGo.transform.localPosition = Vector3.zero;
            //smokeGo.transform.localRotation = Quaternion.identity;

            ParticleSystem smokePs = smokeGo.AddComponent<ParticleSystem>();
            var smokeMain = smokePs.main;
            smokeMain.duration = 0.5f;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            // 연기 파티클이 처음 생성될 때 무작위 각도로 회전된 상태로 시작하도록 설정
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            smokeMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            smokeMain.playOnAwake = false;
            smokeMain.loop = false;
            // 연기는 월드 공간에 남아서 자연스럽게 흩어지도록 World로 설정
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World; 
            smokeMain.stopAction = ParticleSystemStopAction.None;

            var smokeShape = smokePs.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Cone;
            smokeShape.angle = 35f; // 섬광보다 약간 넓게 퍼짐
            smokeShape.radius = 0.2f;

            var smokeEmission = smokePs.emission;
            smokeEmission.rateOverTime = 0;
            smokeEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 3, 6) });

            var smokeRenderer = smokePs.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            // 연기가 생명주기 동안 천천히 회전(롤링)하도록 설정하여 더욱 자연스러운 포연 연출
            var smokeRotCurve = smokePs.rotationOverLifetime;
            smokeRotCurve.enabled = true;
            smokeRotCurve.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

            Material alphaMat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            if (alphaMat.shader == null) alphaMat = new Material(Shader.Find("Particles/Standard Unlit"));
            smokeRenderer.material = alphaMat;

            // 연기는 점차 커짐
            var smokeSizeCurve = smokePs.sizeOverLifetime;
            smokeSizeCurve.enabled = true;
            smokeSizeCurve.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2f));

            // 연기는 서서히 사라짐
            var smokeColorCurve = smokePs.colorOverLifetime;
            smokeColorCurve.enabled = true;
            Gradient smokeGrad = new Gradient();
            smokeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.gray, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            smokeColorCurve.color = new ParticleSystem.MinMaxGradient(smokeGrad);

            // 3. Controller 부착 (부모에만 붙임)
            EffectController controller = go.AddComponent<EffectController>();

            return controller;
        }

        /// <summary>
        /// 캐논 폭발: 직사화기의 빠르고 날카로운 파편 폭발
        /// </summary>
        private EffectController CreateCannonExplosionVfx()
        {
            GameObject go = new GameObject($"PF_Effect_CannonExplosion");
            go.SetActive(false);
            go.transform.SetParent(transform);

            // 1. 순간 섬광 (Core Flash)
            ParticleSystem flashPs = go.AddComponent<ParticleSystem>();
            var main = flashPs.main;
            main.duration = 0.1f;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = 3f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Callback;

            var flashEmission = flashPs.emission;
            flashEmission.rateOverTime = 0;
            flashEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var flashRenderer = flashPs.GetComponent<ParticleSystemRenderer>();
            Material addMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (addMat.shader == null) addMat = new Material(Shader.Find("Particles/Standard Unlit"));
            flashRenderer.material = addMat;

            var flashColor = flashPs.colorOverLifetime;
            flashColor.enabled = true;
            Gradient flashGrad = new Gradient();
            flashGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.5f), 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            flashColor.color = new ParticleSystem.MinMaxGradient(flashGrad);

            // 2. 날카로운 파편 (Sparks)
            GameObject sparksGo = new GameObject("Sparks");
            sparksGo.transform.SetParent(go.transform);
            sparksGo.transform.localPosition = Vector3.zero;
            sparksGo.transform.localRotation = Quaternion.identity; // 부모의 방향(투사체 진행방향)을 그대로 따름

            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            var sparkMain = sparksPs.main;
            sparkMain.duration = 0.3f;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(10f, 25f); // 매우 빠름
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            sparkMain.playOnAwake = false;
            sparkMain.loop = false;
            sparkMain.stopAction = ParticleSystemStopAction.None;
            sparkMain.simulationSpace = ParticleSystemSimulationSpace.World; // 파편이 월드 공간에 남도록

            var sparkShape = sparksPs.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Cone; // 진행 방향(앞쪽)으로 퍼지게 변경
            sparkShape.angle = 45f;
            sparkShape.radius = 0.5f;

            var sparkEmission = sparksPs.emission;
            sparkEmission.rateOverTime = 0;
            sparkEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 30) });

            var sparkRenderer = sparksPs.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch; // 늘어진 선 형태
            sparkRenderer.lengthScale = 2f;
            sparkRenderer.velocityScale = 0.1f;
            sparkRenderer.material = addMat;

            var sparkColor = sparksPs.colorOverLifetime;
            sparkColor.enabled = true;
            Gradient sparkGrad = new Gradient();
            sparkGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0f), new GradientColorKey(Color.red, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sparkColor.color = new ParticleSystem.MinMaxGradient(sparkGrad);

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 박격포 폭발: 곡사화기의 거대한 광역 폭발 (먼지 + 충격파)
        /// </summary>
        private EffectController CreateMortarExplosionVfx()
        {
            GameObject go = new GameObject($"PF_Effect_MortarExplosion");
            go.SetActive(false);
            go.transform.SetParent(transform);

            // 1. 거대 화염 구체 (Blast Core)
            ParticleSystem corePs = go.AddComponent<ParticleSystem>();
            var coreMain = corePs.main;
            coreMain.duration = 0.6f;
            coreMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            coreMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            coreMain.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
            coreMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            coreMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            coreMain.playOnAwake = false;
            coreMain.loop = false;
            coreMain.stopAction = ParticleSystemStopAction.Callback;

            var coreShape = corePs.shape;
            coreShape.shapeType = ParticleSystemShapeType.Sphere;
            coreShape.radius = 1f;

            var coreEmission = corePs.emission;
            coreEmission.rateOverTime = 0;
            coreEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 5, 8) });

            var coreRenderer = corePs.GetComponent<ParticleSystemRenderer>();
            coreRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material alphaMat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            if (alphaMat.shader == null) alphaMat = new Material(Shader.Find("Particles/Standard Unlit"));
            coreRenderer.material = alphaMat;

            var coreSize = corePs.sizeOverLifetime;
            coreSize.enabled = true;
            coreSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f)); // 확 부풀어오름

            var coreColor = corePs.colorOverLifetime;
            coreColor.enabled = true;
            Gradient coreGrad = new Gradient();
            coreGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            coreColor.color = new ParticleSystem.MinMaxGradient(coreGrad);

            // 2. 짙은 흙먼지 (Lingering Smoke)
            GameObject smokeGo = new GameObject("MortarSmoke");
            smokeGo.transform.SetParent(go.transform);
            smokeGo.transform.localPosition = Vector3.zero;
            smokeGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem smokePs = smokeGo.AddComponent<ParticleSystem>();
            var smokeMain = smokePs.main;
            smokeMain.duration = 1.2f;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f); // 넓게 퍼짐
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;
            smokeMain.playOnAwake = false;
            smokeMain.loop = false;
            smokeMain.stopAction = ParticleSystemStopAction.None;

            var smokeShape = smokePs.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Cone;
            smokeShape.angle = 60f; // 넓은 광역
            smokeShape.radius = 1.5f;

            var smokeEmission = smokePs.emission;
            smokeEmission.rateOverTime = 0;
            smokeEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15, 25) });

            var smokeRenderer = smokePs.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            smokeRenderer.material = alphaMat;

            var smokeRot = smokePs.rotationOverLifetime;
            smokeRot.enabled = true;
            smokeRot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var smokeSize = smokePs.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));

            var smokeColor = smokePs.colorOverLifetime;
            smokeColor.enabled = true;
            Gradient smokeGrad = new Gradient();
            smokeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 0f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            smokeColor.color = new ParticleSystem.MinMaxGradient(smokeGrad);

            // 3. 바닥 충격파 (Shockwave Ring)
            GameObject ringGo = new GameObject("Shockwave");
            ringGo.transform.SetParent(go.transform);
            ringGo.transform.localPosition = Vector3.zero;
            ringGo.transform.localRotation = Quaternion.identity;

            ParticleSystem ringPs = ringGo.AddComponent<ParticleSystem>();
            var ringMain = ringPs.main;
            ringMain.duration = 0.3f;
            ringMain.startLifetime = 0.3f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 0.1f;
            ringMain.playOnAwake = false;
            ringMain.loop = false;

            var ringEmission = ringPs.emission;
            ringEmission.rateOverTime = 0;
            ringEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var ringRenderer = ringPs.GetComponent<ParticleSystemRenderer>();
            // 바닥에 깔리도록 HorizontalBillboard
            ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ringRenderer.material = alphaMat; // 투명한 충격 먼지 느낌

            var ringSize = ringPs.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 15f)); // 순식간에 확 퍼짐

            var ringColor = ringPs.colorOverLifetime;
            ringColor.enabled = true;
            Gradient ringGrad = new Gradient();
            ringGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            ringColor.color = new ParticleSystem.MinMaxGradient(ringGrad);

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 잉크 폭탄 착탄 이펙트: 잉크 물보라 + 충격 파티클 (팀 컬러 틴팅 지원)
        /// </summary>
        private EffectController CreateInkBombExplosionVfx()
        {
            GameObject go = new GameObject($"PF_Effect_InkBombExplosion");
            go.SetActive(false);
            go.transform.SetParent(transform);

            // 1. 잉크 방울 폭발 (Splatter Core)
            ParticleSystem corePs = go.AddComponent<ParticleSystem>();
            var coreMain = corePs.main;
            coreMain.duration = 0.6f;
            coreMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            coreMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
            coreMain.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
            coreMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            coreMain.playOnAwake = false;
            coreMain.loop = false;
            coreMain.stopAction = ParticleSystemStopAction.Callback;

            var coreShape = corePs.shape;
            coreShape.shapeType = ParticleSystemShapeType.Sphere;
            coreShape.radius = 0.5f;

            var coreEmission = corePs.emission;
            coreEmission.rateOverTime = 0;
            coreEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15, 25) });

            var coreRenderer = corePs.GetComponent<ParticleSystemRenderer>();
            Material mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            if (mat.shader == null) mat = new Material(Shader.Find("Particles/Standard Unlit"));
            coreRenderer.material = mat;

            var coreColor = corePs.colorOverLifetime;
            coreColor.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            coreColor.color = new ParticleSystem.MinMaxGradient(grad);

            // 2. 바닥 잉크 파동 (Ink Wave Ring)
            GameObject ringGo = new GameObject("InkWaveRing");
            ringGo.transform.SetParent(go.transform);
            ringGo.transform.localPosition = Vector3.zero;

            ParticleSystem ringPs = ringGo.AddComponent<ParticleSystem>();
            var ringMain = ringPs.main;
            ringMain.duration = 0.4f;
            ringMain.startLifetime = 0.4f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 0.1f;
            ringMain.playOnAwake = false;
            ringMain.loop = false;

            var ringEmission = ringPs.emission;
            ringEmission.rateOverTime = 0;
            ringEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var ringRenderer = ringPs.GetComponent<ParticleSystemRenderer>();
            ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ringRenderer.material = mat;

            var ringSize = ringPs.sizeOverLifetime;
            ringSize.enabled = true;
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 18f));

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 유닛 사망시 폭발 이펙트
        /// </summary>        
        private EffectController CreateUnitDeathVfx()
        {
            GameObject go = new GameObject($"PF_Effect_UnitDeath");
            go.SetActive(false);
            go.transform.SetParent(transform);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.startLifetime = 2f; // 이펙트 발동 1초 + 서서히 투명 1초 = 총 2초
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f); // 감속 고려하여 초기 속도 설정
            
            // 가로/세로 무작위 3~6 크기
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(2f, 4f); 
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Callback; // 풀로 자동 반환 트리거

            // 3D 무작위 회전으로 시작
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            // 4~8개 메쉬 파티클 사방으로 퍼짐
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 3, 6) });

            // 물리 요소 없이 무작위 2~5 거리 도달 후 정지하도록 속도 감속 적용
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 10f),
                new Keyframe(0.2f, 2f),
                new Keyframe(0.5f, 0f), // 1초(수명 2초의 50%) 시점에 속도 0
                new Keyframe(1f, 0f)
            ));
            limit.dampen = 0.2f;

            // 파편이 공중에서 회전하며 퍼지는 시각적 효과 (물리 아님)
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rot.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            // 1초 유지 이후 1초 동안 서서히 투명하게 변함
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f), 
                    new GradientAlphaKey(1f, 0.5f), // 1초 시점 (50%)
                    new GradientAlphaKey(0f, 1f)    // 2초 시점 (100%)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            // 메쉬 파티클 객체 설정
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            
            // 그림자 캐스팅 및 수신 활성화
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            
            // 유니티 기본 Cube를 활용하여 메쉬 할당
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            // 빛과 그림자를 지원하는 유니티 6 URP 전용 파티클 셰이더 적용
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            
            // 만약 URP가 아니라면 내장 렌더러용 셰이더로 폴백(Fallback)
            if (mat.shader == null) 
                mat = new Material(Shader.Find("Particles/Standard Surface"));
            
            // URP 셰이더에서 투명도(Fade/Alpha)가 적용되도록 속성 설정
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            renderer.material = mat;

            // --- 중심부 불꽃(Sparks) 이펙트 ---
            GameObject sparksGo = new GameObject("Sparks");
            sparksGo.transform.SetParent(go.transform);
            sparksGo.transform.localPosition = Vector3.zero;
            sparksGo.transform.localRotation = Quaternion.identity;

            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            var sparkMain = sparksPs.main;
            sparkMain.duration = 0.5f;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f); // 생명주기
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f); // 빠르게 튀어나감
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f); // 작은 크기
            sparkMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            sparkMain.playOnAwake = false;
            sparkMain.loop = false;
            sparkMain.stopAction = ParticleSystemStopAction.None; // 부모가 전체 수명을 관리함

            var sparkShape = sparksPs.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.1f;

            var sparkEmission = sparksPs.emission;
            sparkEmission.rateOverTime = 0;
            sparkEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 30) }); // 입자 수

            var sparkRenderer = sparksPs.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch; // 늘어지는 선 형태
            sparkRenderer.lengthScale = 2f;
            sparkRenderer.velocityScale = 0.1f;

            // 밝게 빛나도록 URP 가산 혼합(Additive) 셰이더 적용
            Material sparkMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (sparkMat.shader == null) sparkMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            sparkMat.SetFloat("_Surface", 1); // Transparent
            sparkMat.SetFloat("_Blend", 2); // 2 = Additive
            sparkMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sparkMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            sparkMat.SetInt("_ZWrite", 0);
            sparkMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparkMat.EnableKeyword("_ADDITIVEBLEND_ON");
            sparkMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            sparkRenderer.material = sparkMat;

            // 노란색에서 붉은색으로 변하며 투명해지는 애니메이션
            var sparkColor = sparksPs.colorOverLifetime;
            sparkColor.enabled = true;
            Gradient sparkGrad = new Gradient();
            sparkGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f), new GradientColorKey(Color.red, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sparkColor.color = new ParticleSystem.MinMaxGradient(sparkGrad);
            // ----------------------------------------

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 타워(건물) 폭발 이펙트
        /// </summary>        
        private EffectController CreateTowerDestoryVfx()
        {
            GameObject go = new GameObject($"PF_Effect_TowerDestory");
            go.SetActive(false);
            go.transform.SetParent(transform);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.startLifetime = 2f; // 이펙트 발동 1초 + 서서히 투명 1초 = 총 2초
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f); // 감속 고려하여 초기 속도 설정

            // 가로/세로 무작위 3~6 크기
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Callback; // 풀로 자동 반환 트리거

            // 3D 무작위 회전으로 시작
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            // 4~8개 메쉬 파티클 사방으로 퍼짐
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 3, 6) });

            // 물리 요소 없이 무작위 2~5 거리 도달 후 정지하도록 속도 감속 적용
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 10f),
                new Keyframe(0.2f, 2f),
                new Keyframe(0.5f, 0f), // 1초(수명 2초의 50%) 시점에 속도 0
                new Keyframe(1f, 0f)
            ));
            limit.dampen = 0.2f;

            // 파편이 공중에서 회전하며 퍼지는 시각적 효과 (물리 아님)
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rot.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            // 1초 유지 이후 1초 동안 서서히 투명하게 변함
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f), // 1초 시점 (50%)
                    new GradientAlphaKey(0f, 1f)    // 2초 시점 (100%)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            // 메쉬 파티클 객체 설정
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;

            // 그림자 캐스팅 및 수신 활성화
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // 유니티 기본 Cube를 활용하여 메쉬 할당
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            renderer.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);

            // 빛과 그림자를 지원하는 유니티 6 URP 전용 파티클 셰이더 적용
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));

            // 만약 URP가 아니라면 내장 렌더러용 셰이더로 폴백(Fallback)
            if (mat.shader == null)
                mat = new Material(Shader.Find("Particles/Standard Surface"));

            // URP 셰이더에서 투명도(Fade/Alpha)가 적용되도록 속성 설정
            mat.SetFloat("_Surface", 1); // 1 = Transparent
            mat.SetFloat("_Blend", 0);   // 0 = Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            renderer.material = mat;

            // --- 중심부 불꽃(Sparks) 이펙트 ---
            GameObject sparksGo = new GameObject("Sparks");
            sparksGo.transform.SetParent(go.transform);
            sparksGo.transform.localPosition = Vector3.zero;
            sparksGo.transform.localRotation = Quaternion.identity;

            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            var sparkMain = sparksPs.main;
            sparkMain.duration = 0.5f;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f); // 생명주기
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f); // 빠르게 튀어나감
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f); // 작은 크기
            sparkMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            sparkMain.playOnAwake = false;
            sparkMain.loop = false;
            sparkMain.stopAction = ParticleSystemStopAction.None; // 부모가 전체 수명을 관리함

            var sparkShape = sparksPs.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.1f;

            var sparkEmission = sparksPs.emission;
            sparkEmission.rateOverTime = 0;
            sparkEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 30) }); // 입자 수

            var sparkRenderer = sparksPs.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch; // 늘어지는 선 형태
            sparkRenderer.lengthScale = 2f;
            sparkRenderer.velocityScale = 0.1f;

            // 밝게 빛나도록 URP 가산 혼합(Additive) 셰이더 적용
            Material sparkMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (sparkMat.shader == null) sparkMat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            sparkMat.SetFloat("_Surface", 1); // Transparent
            sparkMat.SetFloat("_Blend", 2); // 2 = Additive
            sparkMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sparkMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            sparkMat.SetInt("_ZWrite", 0);
            sparkMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sparkMat.EnableKeyword("_ADDITIVEBLEND_ON");
            sparkMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            sparkRenderer.material = sparkMat;

            // 노란색에서 붉은색으로 변하며 투명해지는 애니메이션
            var sparkColor = sparksPs.colorOverLifetime;
            sparkColor.enabled = true;
            Gradient sparkGrad = new Gradient();
            sparkGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f), new GradientColorKey(Color.red, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sparkColor.color = new ParticleSystem.MinMaxGradient(sparkGrad);
            // ----------------------------------------

            EffectController controller = go.AddComponent<EffectController>();
            return controller;
        }

        /// <summary>
        /// 이펙트를 지정된 위치와 회전값으로 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position, Quaternion rotation, Color color)
        {
            if (!pools.ContainsKey(type))
            {
                Debug.LogWarning($"[EffectManager] {type} 이펙트 풀이 설정되지 않았습니다.");
                return;
            }

            var effect = pools[type].Get();
            effect.transform.position = position;
            effect.transform.rotation = rotation;
            effect.gameObject.SetActive(true);
            effect.Play(color);
        }

        /// <summary>
        /// 이펙트를 지정된 위치에 재생합니다.
        /// </summary>
        /// <param name="type">이펙트 종류</param>
        /// <param name="position">월드 좌표 위치</param>
        /// <param name="color">진영(팀) 색상 또는 틴트 색상</param>
        public void PlayEffect(EffectType type, Vector3 position, Color color)
        {
            if (!pools.ContainsKey(type))
            {
                //todo manjyn 추가작업
                //Debug.LogWarning($"[EffectManager] {type} 이펙트 풀이 설정되지 않았습니다.");
                return;
            }

            var effect = pools[type].Get();
            effect.transform.position = position;
            effect.gameObject.SetActive(true);
            effect.Play(color);
        }

        /// <summary>
        /// 파티클 재생이 끝났을 때 EffectController에서 호출되는 콜백
        /// </summary>
        private void OnEffectFinished(EffectController effect, EffectType type)
        {
            if (pools.TryGetValue(type, out var pool))
            {
                pool.Release(effect);
            }
            else
            {
                Destroy(effect.gameObject);
            }
        }
    }
}
