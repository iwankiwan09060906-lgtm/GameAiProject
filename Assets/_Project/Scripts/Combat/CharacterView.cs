using UnityEngine;

namespace GameAi
{
    public enum CharacterPose { Idle, Walk }

    // 몸 그림과 손에 든 총을 그린다. 모든 그림은 오른쪽(+X)을 본다
    public sealed class CharacterView : MonoBehaviour
    {
        SpriteRenderer body;
        SpriteRenderer held;
        Sprite idle;
        Sprite shoot;
        Sprite melee;
        Sprite[] walk;
        CharacterViewConfig config;
        float walkClock;
        float shootLeft;
        float meleeLeft;
        float flashLeft;

        public float AimDeg { get; private set; }

        public void Init(string spriteName, AssetCatalog catalog, CharacterViewConfig viewConfig)
        {
            config = viewConfig;
            idle = catalog.GetSprite(spriteName + "_idle");
            shoot = catalog.GetSprite(spriteName + "_shoot");
            walk = catalog.GetFrames(spriteName + "_walk");
            melee = catalog.GetSprite(spriteName + "_melee");
            if (melee == null) melee = shoot;

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sharedMaterial = catalog.SpriteLit;
            body.sortingOrder = SortOrder.Character;
            body.sprite = idle;

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

        public void SetAim(float degrees)
        {
            AimDeg = degrees;
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
        }

        public void SetVisible(bool visible)
        {
            body.enabled = visible;
            held.enabled = visible;
        }

        // 몸 중심에서 본 총구의 월드 위치
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

        public void Flash(float seconds)
        {
            flashLeft = seconds;
            body.color = Color.red;
        }

        public void Tick(float dt, CharacterPose pose)
        {
            shootLeft -= dt;
            meleeLeft -= dt;
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                if (flashLeft <= 0f) body.color = Color.white;
            }

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
