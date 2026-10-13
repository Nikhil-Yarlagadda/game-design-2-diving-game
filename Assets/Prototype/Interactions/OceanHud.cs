using TMPro;
using UnityEngine;

namespace DivingPrototype
{
    [DefaultExecutionOrder(200)]
    public sealed class OceanHud : MonoBehaviour
    {
        public OceanInteractions interactions;
        public TMP_Text status, prompt, message, interior;
        public RectTransform radarRoot;
        public RectTransform hydroxRoot, hydroxFill;
        public Sprite arrowSprite;
        public TMP_FontAsset font;
        [Min(.1f)] public float nearDistance = 10f;
        [Min(.2f)] public float farDistance = 20f;
        private RectTransform[] arrows;
        private TMP_Text[] labels;
        private RadarRings rings;

        public int DistanceBand(float distance) => distance < nearDistance ? 0 : distance < farDistance ? 1 : 2;

        private void OnValidate() => farDistance = Mathf.Max(nearDistance + .1f, farDistance);

        private void Start()
        {
            var guides = new GameObject("Radar Distance Rings", typeof(RectTransform), typeof(RadarRings));
            var rect = guides.GetComponent<RectTransform>();
            rect.SetParent(radarRoot, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rings = guides.GetComponent<RadarRings>();
            rings.color = Color.clear;
            rings.raycastTarget = false;
            arrows = new RectTransform[interactions.targets.Length];
            labels = new TMP_Text[arrows.Length];
            for (int i = 0; i < arrows.Length; i++)
            {
                var target = interactions.targets[i];
                if (target.kind == OceanTarget.TargetKind.MiningSpot) continue;
                var arrow = new GameObject(target.name + " Radar Arrow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                arrows[i] = arrow.GetComponent<RectTransform>();
                arrows[i].SetParent(radarRoot, false);
                arrows[i].sizeDelta = new Vector2(18, 22);
                var image = arrow.GetComponent<UnityEngine.UI.Image>();
                image.sprite = arrowSprite;
                image.useSpriteMesh = true;
                image.raycastTarget = false;
                var outline = arrow.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, .85f);
                outline.effectDistance = new Vector2(1f, -1f);
                var label = new GameObject(target.name + " Radar Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(radarRoot, false);
                labels[i] = label.GetComponent<TextMeshProUGUI>();
                labels[i].font = font;
                labels[i].fontSize = 20;
                labels[i].alignment = TextAlignmentOptions.Center;
                labels[i].raycastTarget = false;
                labels[i].rectTransform.sizeDelta = new Vector2(160, 60);
                labels[i].outlineWidth = .2f;
                labels[i].outlineColor = Color.black;
            }
        }

        private void LateUpdate()
        {
            if (arrows == null) return;
            bool inside = interactions.InsideSubmarine;
            hydroxRoot.gameObject.SetActive(!inside);
            hydroxFill.anchorMax = new Vector2(interactions.Hydrox.NormalizedFill, 1f);
            interior.gameObject.SetActive(inside);
            status.text = "OCEAN EXCURSION\nItems  " + interactions.ItemsCollected + "     Tellurium  " + interactions.Tellurium
                + "     Machines  " + interactions.availableMachines;
            message.text = interactions.Message;
            var target = interactions.Closest;
            prompt.text = inside ? "SPACE  Return to ocean" : target == null ? "" : target.kind == OceanTarget.TargetKind.Submarine
                ? "SPACE  Enter submarine" : target.kind == OceanTarget.TargetKind.Item ? "SPACE  Pick up item"
                : target.HasMachine ? "SPACE  Retrieve machine  |  " + target.YieldAt(Time.time) + " tellurium"
                : "Tier " + target.level + "  |  " + target.level + " / min  |  X  Place machine";
            if (!inside) status.text += "\nWASD  Swim    SPACE  Interact    X  Mine\nTap Z  Prospect"
                + (interactions.CooldownRemaining > 0f ? " (" + interactions.CooldownRemaining.ToString("0.0") + "s)" : " (ready)")
                + "    Hold Z  Radar";
            rings.gameObject.SetActive(!inside && interactions.RadarActive);
            if (interactions.RadarActive && !inside)
                status.text += "\nRadar rings: <" + nearDistance.ToString("0.#") + "m  |  "
                    + nearDistance.ToString("0.#") + "-" + farDistance.ToString("0.#") + "m  |  "
                    + farDistance.ToString("0.#") + "m+";
            Vector2 origin = interactions.oceanCamera.WorldToScreenPoint(interactions.transform.position);
            OceanTarget nearestItem = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var item in interactions.targets)
            {
                if (item == null || !item.isActiveAndEnabled || item.kind != OceanTarget.TargetKind.Item) continue;
                float distance = Vector2.Distance(interactions.transform.position, item.transform.position);
                if (distance < nearestDistance) { nearestDistance = distance; nearestItem = item; }
            }
            Vector2 submarineLabel = Vector2.zero;
            bool hasSubmarineLabel = false;
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] == null) continue;
                var tracked = interactions.targets[i];
                bool visible = !inside && interactions.RadarActive && tracked != null && tracked.isActiveAndEnabled;
                arrows[i].gameObject.SetActive(visible);
                bool showLabel = visible && (tracked.kind == OceanTarget.TargetKind.Submarine || tracked == nearestItem);
                labels[i].gameObject.SetActive(showLabel);
                if (!visible) continue;
                Vector2 direction = ((Vector2)interactions.oceanCamera.WorldToScreenPoint(tracked.transform.position) - origin).normalized;
                if (direction == Vector2.zero) direction = Vector2.up;
                float distance = Vector2.Distance(interactions.transform.position, tracked.transform.position);
                int band = DistanceBand(distance);
                float lane = RadarRings.Radius(radarRoot.rect.size, band);
                arrows[i].sizeDelta = band == 0 ? new Vector2(22, 26)
                    : band == 1 ? new Vector2(16, 20) : new Vector2(12, 14);
                arrows[i].anchoredPosition = direction * lane;
                arrows[i].localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                Vector2 labelPosition = direction * (lane + 55f);
                if (tracked.kind == OceanTarget.TargetKind.Submarine)
                {
                    submarineLabel = labelPosition;
                    hasSubmarineLabel = true;
                }
                else if (showLabel && hasSubmarineLabel && Mathf.Abs(labelPosition.x - submarineLabel.x) < 160f
                    && Mathf.Abs(labelPosition.y - submarineLabel.y) < 65f)
                    labelPosition.y = submarineLabel.y + (submarineLabel.y > 0f ? -70f : 70f);
                labelPosition.x = Mathf.Clamp(labelPosition.x, -radarRoot.rect.width * .5f + 85f, radarRoot.rect.width * .5f - 85f);
                labelPosition.y = Mathf.Clamp(labelPosition.y, -radarRoot.rect.height * .5f + 35f, radarRoot.rect.height * .5f - 35f);
                labels[i].rectTransform.anchoredPosition = labelPosition;
                labels[i].text = tracked.displayName + "\n" + distance.ToString("0") + "m";
                labels[i].color = tracked.kind == OceanTarget.TargetKind.Submarine ? new Color(.55f, .9f, 1f) : Color.white;
                Color arrowColor = labels[i].color;
                arrowColor.a = band == 0 ? 1f : band == 1 ? .7f : .45f;
                arrows[i].GetComponent<UnityEngine.UI.Image>().color = arrowColor;
            }
        }
    }
}
