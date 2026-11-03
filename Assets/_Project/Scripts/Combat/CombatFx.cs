using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameAi
{
    // 총구 화염·궤적·탄피·혈흔·시체 연출(FN-COM-04). 이벤트를 받아서 만든다.
    // 모양을 고르는 난수는 연출 난수원(Rng.Fx)만 쓴다. AI 난수를 쓰면 재현성이 깨진다.
    // 개수가 정해진 풀로 재사용하므로 아무리 쏴도 메모리가 늘지 않는다
    public sealed class CombatFx : MonoBehaviour
    {
        // 잠깐 보였다 사라지는 연출은 이 개수면 충분하다
        const int ShortLivedPool = 32;
        const int LightPool = 8;

        // 피 튀는 방향은 전방향
        const float BloodScatterAngle = 360f;

        // 정해진 시간 뒤에 꺼지는 연출 하나(궤적선, 총구 화염)
        sealed class Timed
        {
            public SpriteRenderer Renderer;
            public float Left;
        }

        // 피격 순간의 피 애니메이션(프레임을 번호 순서로 재생)
        sealed class HitBlood
        {
            public SpriteRenderer Renderer;
            public float Clock;
            public bool Active;
        }

        // 총구에서 바닥으로 튀는 탄피
        sealed class Flight
        {
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 To;
            public float Clock;
            public bool Active;
        }

        sealed class MuzzleLight
        {
            public Light2D Light;
            public float Left;
        }

        AssetCatalog catalog;
        FxConfig fx;
        LightingConfig lighting;
        Sprite[] muzzleSprites;
        Sprite[] bloodSprites;
        Sprite[] hitFrames;
        Sprite casingSprite;
        Sprite whitePixel;
        Color tracerColor;

        // 시간 갱신이 필요한 연출 목록(풀에서 새로 만들 때마다 여기에도 등록한다)
        readonly List<Timed> timed = new List<Timed>();
        readonly List<HitBlood> hitBloods = new List<HitBlood>();
        readonly List<Flight> flights = new List<Flight>();
        readonly List<MuzzleLight> lights = new List<MuzzleLight>();

        // 바닥에 남는 것(혈흔·탄피)은 최대 512개 풀. 시체는 풀에 넣지 않는다(미션 동안 모두 남는다)
        ObjectPool<SpriteRenderer> decals;
        ObjectPool<Timed> tracers;
        ObjectPool<Timed> flashes;
        ObjectPool<HitBlood> hitPool;
        ObjectPool<Flight> flightPool;
        ObjectPool<MuzzleLight> lightPool;

        public int DecalCount { get { return decals.Count; } }
        public int CorpseCount { get; private set; }

        public void Init(AssetCatalog assets, GameConfig config)
        {
            catalog = assets;
            fx = config.fx;
            lighting = config.lighting;
            muzzleSprites = assets.GetVariants("muzzle_flash");
            bloodSprites = assets.GetVariants("blood");
            hitFrames = assets.GetVariants("hit_blood");
            casingSprite = assets.GetSprite("casing");
            whitePixel = ProceduralSprites.Get("white");
            tracerColor = MathUtil.ParseColor(fx.tracer_color, new Color(1f, 0.9f, 0.5f));

            decals = new ObjectPool<SpriteRenderer>(fx.pool_max, () => CreateRenderer("Decal", assets.SpriteLit, SortOrder.Decal));
            tracers = new ObjectPool<Timed>(ShortLivedPool, () => AddTimed(CreateRenderer("Tracer", assets.SpriteUnlit, SortOrder.Fx)));
            flashes = new ObjectPool<Timed>(ShortLivedPool, () => AddTimed(CreateRenderer("Muzzle", assets.SpriteUnlit, SortOrder.Fx)));
            hitPool = new ObjectPool<HitBlood>(ShortLivedPool, CreateHitBlood);
            flightPool = new ObjectPool<Flight>(ShortLivedPool, CreateFlight);
            lightPool = new ObjectPool<MuzzleLight>(LightPool, CreateLight);
        }

        // 구독은 OnEnable, 해제는 OnDisable(5.1)
        void OnEnable()
        {
            EventBus.Subscribe<ShotFiredEvent>(OnShot);
            EventBus.Subscribe<DamageEvent>(OnDamage);
            EventBus.Subscribe<DeathEvent>(OnDeath);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ShotFiredEvent>(OnShot);
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
            EventBus.Unsubscribe<DeathEvent>(OnDeath);
        }

        SpriteRenderer CreateRenderer(string objectName, Material material, int order)
        {
            var go = new GameObject(objectName, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        // 새로 만든 연출을 갱신 목록에 등록한다. 처음에는 꺼 둔다
        Timed AddTimed(SpriteRenderer renderer)
        {
            renderer.enabled = false;
            var item = new Timed { Renderer = renderer };
            timed.Add(item);
            return item;
        }

        HitBlood CreateHitBlood()
        {
            SpriteRenderer renderer = CreateRenderer("HitBlood", catalog.SpriteLit, SortOrder.Fx);
            renderer.enabled = false;
            var item = new HitBlood { Renderer = renderer };
            hitBloods.Add(item);
            return item;
        }

        Flight CreateFlight()
        {
            var item = new Flight { Renderer = null };
            flights.Add(item);
            return item;
        }

        // 총구 화염 빛: 작은 점광원. 쏠 때만 잠깐 켠다
        MuzzleLight CreateLight()
        {
            var go = new GameObject("MuzzleLight");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = MathUtil.ParseColor(lighting.muzzle_light_color, Color.yellow);
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = lighting.muzzle_light_radius_m;
            light.falloffIntensity = lighting.lamp_falloff;
            light.shadowsEnabled = true;
            light.shadowIntensity = lighting.shadow_strength;
            light.enabled = false;
            var item = new MuzzleLight { Light = light };
            lights.Add(item);
            return item;
        }

        // 사격 연출 4가지를 한꺼번에 만든다
        void OnShot(ShotFiredEvent evt)
        {
            float angle = MathUtil.AngleDeg(evt.dir);
            ShowFlash(evt.muzzlePos, angle);
            ShowTracer(evt.muzzlePos, evt.endPos);
            ShowLight(evt.muzzlePos);
            ThrowCasing(evt.muzzlePos, evt.dir);
        }

        // 총구 화염: 비슷한 그림 여러 장 중 하나를 연출 난수로 고른다
        void ShowFlash(Vector2 muzzle, float angle)
        {
            Timed flash = flashes.Acquire();
            Sprite sprite = muzzleSprites[Rng.Fx.Int(0, muzzleSprites.Length)];
            flash.Renderer.sprite = sprite;
            flash.Renderer.enabled = true;
            flash.Left = fx.muzzle_s;

            // 그림 왼쪽 끝이 총구에 오도록 폭의 절반만큼 앞으로 민다
            Vector2 forward = MathUtil.FromAngleDeg(angle) * sprite.bounds.extents.x;
            flash.Renderer.transform.SetPositionAndRotation(muzzle + forward, Quaternion.Euler(0f, 0f, angle));
        }

        // 총알 궤적선: 흰 픽셀 1장을 총구~끝 길이로 늘려 그린다
        void ShowTracer(Vector2 from, Vector2 to)
        {
            Timed tracer = tracers.Acquire();
            Vector2 delta = to - from;
            tracer.Renderer.sprite = whitePixel;
            tracer.Renderer.color = tracerColor;
            tracer.Renderer.enabled = true;
            tracer.Left = fx.tracer_s;
            Transform t = tracer.Renderer.transform;
            t.SetPositionAndRotation((from + to) / 2f, Quaternion.Euler(0f, 0f, MathUtil.AngleDeg(delta)));

            // 가로는 거리만큼, 세로는 1픽셀(1/32m)
            t.localScale = new Vector3(delta.magnitude, 1f / 32f, 1f);
        }

        // 총구 빛을 잠깐 켠다
        void ShowLight(Vector2 position)
        {
            MuzzleLight item = lightPool.Acquire();
            item.Left = lighting.muzzle_light_s;
            item.Light.transform.position = position;
            item.Light.enabled = true;
        }

        // 탄피는 총의 오른쪽 뒤로 튀어 바닥에 남는다. 난수로 거리와 방향을 조금씩 바꾼다
        void ThrowCasing(Vector2 muzzle, Vector2 dir)
        {
            SpriteRenderer casing = decals.Acquire();
            casing.sprite = casingSprite;
            casing.color = Color.white;
            casing.transform.localScale = Vector3.one;
            casing.transform.rotation = Quaternion.identity;

            // 총 방향의 오른쪽 수직 방향이 기준
            Vector2 side = new Vector2(dir.y, -dir.x);
            float distance = Rng.Fx.Range(fx.casing_dist_min_m, fx.casing_dist_max_m);
            Vector2 throwDir = MathUtil.Rotate(side, Rng.Fx.Range(-35f, 35f));

            Flight flight = flightPool.Acquire();
            flight.Renderer = casing;
            flight.From = muzzle;
            flight.To = muzzle + throwDir * distance;
            flight.Clock = 0f;
            flight.Active = true;
            casing.transform.position = muzzle;
        }

        // 총알이 맞았을 때: 피 애니메이션 + 바닥 혈흔 하나. 근접 제압은 피를 튀기지 않는다
        void OnDamage(DamageEvent evt)
        {
            if (evt.kind != DamageKind.Bullet) return;

            HitBlood hit = hitPool.Acquire();
            hit.Clock = 0f;
            hit.Active = true;
            hit.Renderer.enabled = true;
            hit.Renderer.sprite = hitFrames[0];
            hit.Renderer.transform.SetPositionAndRotation(evt.hitPoint, Quaternion.Euler(0f, 0f, Rng.Fx.Range(0f, 360f)));

            // 맞은 자리 근처에 혈흔이 하나 남는다
            Vector2 offset = MathUtil.FromAngleDeg(Rng.Fx.Range(0f, BloodScatterAngle)) * Rng.Fx.Range(0f, fx.blood_scatter_m);
            PlaceBlood(evt.hitPoint + offset, Rng.Fx.Range(fx.blood_scale_min, fx.blood_scale_max));
        }

        // 바닥 혈흔: 그림·각도·크기를 난수로 바꿔 놓는다
        void PlaceBlood(Vector2 position, float scale)
        {
            SpriteRenderer blood = decals.Acquire();
            blood.sprite = bloodSprites[Rng.Fx.Int(0, bloodSprites.Length)];
            blood.color = Color.white;
            blood.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Rng.Fx.Range(0f, 360f)));
            blood.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // 사망: 죽은 자리에 시체 그림을 놓는다. 시체는 풀에 넣지 않아 미션이 끝날 때까지 모두 남는다
        void OnDeath(DeathEvent evt)
        {
            SpriteRenderer corpse = CreateRenderer("Corpse", catalog.SpriteLit, SortOrder.Corpse);
            corpse.sprite = catalog.GetSprite(evt.corpseSprite);
            corpse.transform.SetPositionAndRotation(evt.position, Quaternion.Euler(0f, 0f, evt.rotationDeg));
            CorpseCount++;
        }

        // 9단계: 시간에 맞춰 연출을 끄고 갱신한다. 대화·일시정지 중에도 불린다
        public void Tick(float dt)
        {
            // 궤적선·총구 화염: 시간이 다 되면 끈다
            for (int i = 0; i < timed.Count; i++)
            {
                Timed item = timed[i];
                if (item.Left <= 0f) continue;
                item.Left -= dt;
                if (item.Left <= 0f) item.Renderer.enabled = false;
            }

            for (int i = 0; i < lights.Count; i++)
            {
                MuzzleLight item = lights[i];
                if (item.Left <= 0f) continue;
                item.Left -= dt;
                if (item.Left <= 0f) item.Light.enabled = false;
            }

            // 피 애니메이션: 재생 시간(0.3초) 동안 프레임을 번호 순서로 넘긴다
            for (int i = 0; i < hitBloods.Count; i++)
            {
                HitBlood item = hitBloods[i];
                if (!item.Active) continue;
                item.Clock += dt;
                int frame = Mathf.FloorToInt(item.Clock / fx.hit_blood_s * hitFrames.Length);
                if (frame >= hitFrames.Length)
                {
                    item.Active = false;
                    item.Renderer.enabled = false;
                }
                else
                {
                    item.Renderer.sprite = hitFrames[frame];
                }
            }

            // 탄피: 총구에서 바닥 목표 지점까지 짧게 날아간 뒤 그 자리에 남는다
            for (int i = 0; i < flights.Count; i++)
            {
                Flight item = flights[i];
                if (!item.Active) continue;
                item.Clock += dt;
                float t = Mathf.Clamp01(item.Clock / fx.casing_s);
                item.Renderer.transform.position = Vector2.Lerp(item.From, item.To, t);
                if (t >= 1f) item.Active = false;
            }
        }
    }
}
