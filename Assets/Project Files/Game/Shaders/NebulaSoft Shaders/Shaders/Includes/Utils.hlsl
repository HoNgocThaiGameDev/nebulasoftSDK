#ifndef W_UNILS
#define W_UNILS

    half Remap(half In, half2 InMinMax, half2 OutMinMax)
    {
        return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
    }

#endif