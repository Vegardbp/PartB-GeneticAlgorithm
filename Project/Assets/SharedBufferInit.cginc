//This holds the neccesary buffer initializations that can be simply included for each compute shader that needs them

struct Individual
{
    float position[500];
    //unfortunatley, dynamic size buffers don't exist on the GPU, and it therefore has to be defined here
    //however I can define a dimention higher than the default, that way I can change it on the cpu side
};

RWStructuredBuffer<Individual> populationBuffer;
RWStructuredBuffer<float> fitnessBuffer;
RWStructuredBuffer<float> childFitnessBuffer;
RWStructuredBuffer<int> tournamentWinners;
RWStructuredBuffer<Individual> childBuffer;
int populationSize;
int dimension;

static const float PI = 3.141592653589793;

void CalculateFitness(int index, bool child) //function to get fitness here, as this will be used 2 diffrenet places
{
    //get the position
    Individual individual;
    if (child)
        individual = childBuffer[index];
    else
        individual = populationBuffer[index];

    //Rastrigin function
    float fitness = 10.0 * dimension;
    for (int i = 0; i < dimension; i++)
    {
        float xi = individual.position[i];
        fitness += pow(xi,2.0) - 10.0 * cos(2.0 * PI * xi);
    }
    
    //set the fitness
    if (child)
        childFitnessBuffer[index] = fitness;
    else
        fitnessBuffer[index] = fitness;
}