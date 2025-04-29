using UnityEngine;

namespace FFT
{
    public class ComputeKernel
    {
        private ComputeShader shader;
        int kernel;
        GroupSize groupSize;
        DispatchSize dispatchSize;
        
        private struct GroupSize
        {
            public uint x;
            public uint y;
            public uint z;

            public GroupSize(ComputeShader shader, int kernel)
            {
                shader.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            }
        }

        private struct DispatchSize
        {
            public int x;
            public int y;
            public int z;

            public void Update(GroupSize groupSize, Vector3Int threadSize)
            {
                x = Mathf.CeilToInt(threadSize.x / (float)groupSize.x);
                y = Mathf.CeilToInt(threadSize.y / (float)groupSize.y);
                z = Mathf.CeilToInt(threadSize.z / (float)groupSize.z);
            }
        }
        
        ComputeKernel(ComputeShader shader, int kernel)
        {
            this.shader = shader;
            this.kernel = kernel;
            groupSize = new GroupSize(shader, kernel);
        }
    }
}