using UnityEngine;

namespace GameAi
{
    // 캐릭터가 서 있는지 걷는지
    public enum CharacterPose { Idle, Walk }

    // 몸 그림과 손에 든 총을 그린다. 모든 그림은 오른쪽(+X)을 보고 있으므로,
    // 게임 오브젝트를 조준 각도만큼 돌리면 방향이 맞는다. 총은 몸의 자식이라 같이 돈다
    public sealed class CharacterView : MonoBehaviour
    {
        SpriteRenderer body;
        SpriteRenderer held;
        Sprite idle;
        Sprite shoot;
        Sprite melee;
        Sprite[] walk;
        CharacterViewConfig config;

        // 걷기 애니메이션이 흐른 시간
        float walkClock;

        // 사격·근접 그림을 보여 줄 남은 시간. 0 이하면 끝
        float shootLeft;
        float meleeLeft;

        // 피격으로 빨갛게 보일 남은 시간
        float flashLeft;

        // 지금 바라보는 각도(도). 시체가 쓰러지는 방향으로도 쓴다
        public float AimDeg { get; private set; }

        // spriteName은 "player", "guard" 같은 접두사. {이름}_idle 등을 카탈로그에서 찾는다
        public void Init(string spriteName, AssetCatalog catalog, CharacterViewConfig viewConfig)
        {
            config = viewConfig;
            idle = catalog.GetSprite(spriteName + "_idle");
            shoot = catalog.GetSprite(spriteName + "_shoot");
            walk = catalog.GetFrames(spriteName + "_walk");

            // 근접 그림이 없는 캐릭터(적)는 사격 그림으로 대신한다
            melee = catalog.GetSprite(spriteName + "_melee");
            if (melee == null) melee = shoot;

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sharedMaterial = catalog.SpriteLit;
            body.sortingOrder = SortOrder.Character;
            body.sprite = idle;

            // 손에 든 총: 몸의 자식으로 손 위치에 붙인다. 위치는 총 그림 중심 기준이라 총 길이의 절반을 더한다
            var heldObject = new GameObject("Held", typeof(SpriteRenderer));
            heldObject.transform.SetParent(transform, false);
            heldObject.transform.localPosition = new Vector3(viewConfig.hand_x_m + viewConfig.held_center_m, viewConfig.hand_y_m, 0f);
            held = heldObject.GetComponent<SpriteRenderer>();
            held.sharedMaterial = catalog.SpriteLit;
            held.sortingOrder = SortOrder.Held;
        }

        public void SetHeld(Sprite sprite)
        {
            held.sprite = sprite;
        }

        // 바라보는 각도를 정하고 몸을 돌린다
        public void SetAim(float degrees)
        {
            AimDeg = degrees;
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
        }

        // 시야 밖이면 숨긴다
        public void SetVisible(bool visible)
        {
            body.enabled = visible;
            held.enabled = visible;
        }

        // 총구의 월드 위치. 손 위치에서 총구까지의 거리를 현재 각도로 돌려서 구한다
        public Vector2 MuzzlePosition(Vector2 center)
        {
            var local = new Vector2(config.hand_x_m + config.muzzle_m, config.hand_y_m);
            return center + MathUtil.Rotate(local, AimDeg);
        }

        public void PlayShoot()
        {
            shootLeft = config.shoot_frame_s;
        }

        public void PlayMelee()
        {
            meleeLeft = config.melee_frame_s;
        }

        // 피격 깜빡임: 즉시 빨갛게 바꾸고, Tick에서 시간이 지나면 원래 색으로 돌린다
        public void Flash(float seconds)
        {
            flashLeft = seconds;
            body.color = Color.red;
        }

        // 지금 보여 줄 그림을 고른다. 근접 > 사격 > 걷기 > 서기 순으로 우선한다
        public void Tick(float dt, CharacterPose pose)
        {
            shootLeft -= dt;
            meleeLeft -= dt;
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                if (flashLeft <= 0f) body.color = Color.white;
            }

            // 멈추면 걷기 애니메이션을 처음부터 다시 시작한다
            if (pose == CharacterPose.Walk) walkClock += dt;
            else walkClock = 0f;

            if (meleeLeft > 0f) body.sprite = melee;
            else if (shootLeft > 0f) body.sprite = shoot;
            else if (pose == CharacterPose.Walk && walk.Length > 0)
                body.sprite = walk[(int)(walkClock * config.walk_fps) % walk.Length];
            else body.sprite = idle;
        }
    }
}
