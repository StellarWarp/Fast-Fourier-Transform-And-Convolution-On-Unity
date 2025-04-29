using UnityEngine;

namespace TestScript
{
    public class CameraMoveTest : MonoBehaviour
    {
        public float orbitRadius = 2.0f;           // 圆周运动半径
        public float orbitSpeed = 10.0f;           // 每秒多少度
        public bool lookAtCenter = false;           // 是否一直看向圆心

        private Vector3 center;                    // 圆心
        private Vector3 right;                     // 摄像机右方向
        private Vector3 up;                        // 摄像机上方向
        private float angle = 0f;

        void Start()
        {
            // 初始化圆心为当前位置
            center = transform.position;

            // 获取摄像机当前的方向（视线方向）
            Vector3 forward = transform.forward;

            // 构造垂直于forward的两个方向（在这个平面内运动）
            right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right == Vector3.zero)
            {
                // forward 和 up 平行，无法叉积，用默认右方向
                right = Vector3.right;
            }
            up = Vector3.Cross(forward, right).normalized;

            // 把位置移到圆周起点
            transform.position = center + right * orbitRadius;
        }

        void Update()
        {
            angle += orbitSpeed * Time.deltaTime;

            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = Mathf.Cos(rad) * right * orbitRadius + Mathf.Sin(rad) * up * orbitRadius;
            transform.position = center + offset;

            if (lookAtCenter)
            {
                transform.LookAt(center);
            }
        }
    }
}