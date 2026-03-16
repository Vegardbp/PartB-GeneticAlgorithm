//hashing functions
//Generating a random number on the GPU is not entirley trivial, therefore, it gets a single random number (the seed)
//from the CPU, and generates one seemingly random number per integer based on the seed.
//Threads can then pass their ID to this to get a "random" number

int seed;

float hash(uint x){ //I'm not sure if this was AI generated or not, it is from one of my old projects
    int localSeed = seed;
    localSeed *= x;
    localSeed = (localSeed ^ 61) ^ (localSeed >> 16);
    localSeed *= 9;
    localSeed = localSeed ^ (localSeed >> 4);
    localSeed *= 0x27d4eb2d;
    localSeed = localSeed ^ (localSeed >> 15);
    float outVal = localSeed / 10000000.0f;
    outVal -= (int)outVal;
    return outVal;
}

int hashIntRange(uint x, int min, int max)
{
    int localSeed = seed;
    localSeed *= x;
    localSeed = (localSeed ^ 61) ^ (localSeed >> 16);
    localSeed *= 9;
    localSeed = localSeed ^ (localSeed >> 4);
    localSeed *= 0x27d4eb2d;
    localSeed = localSeed ^ (localSeed >> 15);

    int range = max - min + 1;
    return min + (abs(localSeed) % range);
}
