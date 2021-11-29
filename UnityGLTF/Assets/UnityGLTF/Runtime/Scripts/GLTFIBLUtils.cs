using UnityEngine.Rendering;

public class GLTFIBLUtils
{

  public static double[][] ExtractSHCoefficients(SphericalHarmonicsL2 probeSH)
  {

    // https://docs.unity3d.com/Manual/LightProbes-TechnicalInformation.html
    //                         [L00:  DC]

    //             [L1-1:  y] [L10:   z] [L11:   x]

    //   [L2-2: xy] [L2-1: yz] [L20:  zz] [L21:  xz]  [L22:  xx - yy]


    // L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22, // red

    // L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22, // blue

    // L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22  // green

    double[][] irradiance = new double[9][];
    for (int i = 0; i <= 8; i++)
    {

      // Left handed export
      int tgt = i;
      switch (tgt)
      {
        case 3:
          tgt = 4;
          break;
        case 4:
          tgt = 3;
          break;
        default:
          break;
      }

      irradiance[tgt] = new double[3]{
                        probeSH[0, i],
                        probeSH[1, i],
                        probeSH[2, i]
                    };

    }



    return irradiance;

  }

}
