using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Globalization;

//the controller

public class ComputeManager : MonoBehaviour
{
    const int maxDimension = 500; //since dynamic length arrays don't exist on the GPU, this has to be defined here

    //parameters for the GA, these can be set with in program UI
    public int dimension = 100;
    public int populationSize = 10;
    public float successThrshold = 0.0001f;
    public float searchSpaceSize = 5.12f;
    public float explorationFalloff = 1.0f;

    //dependencies and such I need to drag and drop in the unity editor
    public ComputeShader FitnessCalculator;
    public ComputeShader TournamentRunner;
    public ComputeShader Breed;
    public ComputeShader PushChildren;
    public ComputeShader PersistElites;
    public LineRenderer graphLine;

    //dependencies for UI
    public TMP_Text generationText;
    public TMP_Text fitnessText;
    public TMP_Text timeText;

    public TMP_InputField population;
    public TMP_InputField dim;
    public TMP_InputField threshold; 
    public TMP_InputField MutationScale;
    public TMP_InputField MutationFalloff;

    public Button runBtn;
    public Button stop;

    public Toggle logToggle;


    //definitions of all compute buffers, these need to exist represented on the CPU for the GPU to be able to use them
    ComputeBuffer populationBuffer;
    ComputeBuffer fitnessBuffer; //This is seperate so the fitnesses can be passed to the CPU without the positions
    ComputeBuffer tournamentWinners;
    ComputeBuffer childBuffer;
    ComputeBuffer childFitnessBuffer;

    //integer to define how many threads must be dispatched. defined in initialization
    int dispatchSize;

    //previously used for testing
    public bool reInitalize = false;
    public bool run = true;

    //variables used to meassure performance
    int generations = 0;
    float time = 0;

    List<float> fitnessValues = new();

    void Start()
    {
        //initialize all the required stuff right away
        Initialize();

        //delegates to handle buttons and value changes during runtime
        threshold.onValueChanged.AddListener((string text) =>
        {
            run = false;
        });
        population.onValueChanged.AddListener((string text) =>
        {
            run = false;
        });
        runBtn.onClick.AddListener(() =>
        {
            Initialize();
            run = true;
        });
        stop.onClick.AddListener(() =>
        {
            Initialize();
            run = false;
        });

    }

    void Update()
    {
        RenderGraph();
        if (reInitalize)
        {
            reInitalize = false;
            Initialize();
        }
        if (run)
            Run();
    }

    void RenderGraph() //function to render all the meassured fitness points in either linear or logarithmic scale
    {
        //parts of this function was made by AI

        bool log = logToggle.isOn; //whether or not its in logarithmic scale
        if (fitnessValues.Count <= 0) return;

        graphLine.positionCount = fitnessValues.Count;

        float maxValue = Mathf.Max(fitnessValues.ToArray()); // Get max value for normalization
        float minValue = Mathf.Min(fitnessValues.ToArray()); // Get min value to shift the log scale

        if (minValue <= 0) minValue = 0.01f; // Prevent log(0) or negative values
        if (maxValue <= 0) maxValue = 1f; // Prevent log issues if all values are zero

        float logMaxY = Mathf.Log(maxValue + 1); // Log max for y-axis normalization
        float logMaxX = Mathf.Log(fitnessValues.Count); // Log max for x-axis normalization
        if (!log)
        {
            logMaxY = maxValue + 1;
            logMaxX = fitnessValues.Count;
        }

        for (int i = 0; i < fitnessValues.Count; i++)
        {
            // Log-scale x-axis
            float logX = Mathf.Log(i + 1); // Use (i + 1) to prevent log(0)
            if (!log)
                logX = i + 1;
            float x = Mathf.Lerp(-8f, 8f, logX / logMaxX);

            // Log-scale y-axis
            float logY = Mathf.Log(fitnessValues[i] + 1); // Prevent log(0)
            if (!log)
                logY = fitnessValues[i] + 1;
            float y = Mathf.Lerp(-4f, 4f, logY / logMaxY);

            graphLine.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    float bestFitness = 1000;
    void Run()
    {
        //read data from input fields
        successThrshold = float.Parse(threshold.text, CultureInfo.InvariantCulture);

        float exploration = float.Parse(MutationScale.text, CultureInfo.InvariantCulture);
        explorationFalloff = float.Parse(MutationFalloff.text, CultureInfo.InvariantCulture);


        time += Time.deltaTime;
        generations++;

        //Dispatch all the compute shaders in order
        Dispatch(ref TournamentRunner);
        exploration *= 1.0f-Mathf.Clamp(Mathf.Exp(-bestFitness * explorationFalloff),0.0f,0.95f);
        exploration *= Random.Range(0.8f, 1.2f);
        Breed.SetFloat("sigma", exploration);
        Dispatch(ref Breed);
        Dispatch(ref PersistElites);
        Dispatch(ref PushChildren);

        //get the result of the generation
        float fitness = Mathf.Round(GetBestFitnesses() * 100000f) / 100000f;

        //update UI
        generationText.text = "Generations: " + generations.ToString();
        timeText.text = "Time: " + time.ToString();
        fitnessText.text = "Current fitness: " + fitness.ToString();

        //Add value for graphing
        fitnessValues.Add(fitness);

        //update best fitness
        bestFitness = Mathf.Min(bestFitness, fitness);

        //stopping criteria
        if (fitness < successThrshold)
            run = false;
    }

    void Dispatch(ref ComputeShader shader) //Helper function to ensure all compute shaders are dispatched correctly
    {
        PassNeccesaryBuffers(ref shader);
        shader.Dispatch(0, dispatchSize, 1, 1);
    }

    float GetBestFitnesses()
    {
        float[] fitnesses = new float[populationSize]; //define an empty array of the correct size
        fitnessBuffer.GetData(fitnesses); //fill the array by GPU readback
        return Mathf.Min(fitnesses); //get the best fitness from this generation
    }

    void PassNeccesaryBuffers(ref ComputeShader shader) //helper function to ensure all compute shaders get the correct buffers in the correct way
    {
        shader.SetBuffer(0, "populationBuffer", populationBuffer);
        shader.SetBuffer(0, "fitnessBuffer", fitnessBuffer);
        shader.SetBuffer(0, "tournamentWinners", tournamentWinners);
        shader.SetBuffer(0, "childBuffer", childBuffer);
        shader.SetBuffer(0, "childFitnessBuffer", childFitnessBuffer);
        shader.SetInt("populationSize", populationSize);
        shader.SetInt("dimension", dimension);
        shader.SetInt("seed", Random.Range(1, 1000));
    }

    void Initialize()
    {
        //read dimension and limit it to the max the GPU can handle
        dimension = (int)System.Convert.ToSingle(dim.text);
        dimension = Mathf.Clamp(dimension, 1, maxDimension);
        dim.text = dimension.ToString();

        //read population size from UI
        populationSize = (int)System.Convert.ToSingle(population.text);

        //reset the neccessary stuff
        time = 0;
        generations = 0;
        fitnessValues.Clear();
        bestFitness = 10000000;

        //allocate the population buffer
        populationBuffer?.Release();
        //the first value "populationSize", is how many elements the buffer has, the second is the stride, aka, how big each element is in bytes
        populationBuffer = new ComputeBuffer(populationSize, maxDimension * sizeof(float));

        //initialize random positions as a blittable list
        float[] positions = new float[populationSize* maxDimension];
        for(int i = 0; i < positions.Length; i++)
            positions[i] = Random.Range(-searchSpaceSize, searchSpaceSize);

        populationBuffer.SetData(positions); //fill the population buffer

        //allocate the fitness buffer
        fitnessBuffer?.Release();
        fitnessBuffer = new ComputeBuffer(populationSize, sizeof(float));

        //allocate the tournament buffer
        tournamentWinners?.Release();
        tournamentWinners = new ComputeBuffer(populationSize, sizeof(float));

        //you get the idea
        childBuffer?.Release();
        childBuffer = new ComputeBuffer(populationSize, maxDimension * sizeof(float));

        childFitnessBuffer?.Release();
        childFitnessBuffer = new ComputeBuffer(populationSize, sizeof(float));

        //define the size of each dispatch
        dispatchSize = Mathf.CeilToInt(populationSize / 528.0f); //make sure there are enough by Ceiling the value

        //get the initial fitness values
        Dispatch(ref FitnessCalculator); //initialize all the fitness values
    }


    //de-allocate buffers to avoid memory leak
    void ReleaseBuffers()
    {
        populationBuffer?.Release();
        fitnessBuffer?.Release();
        tournamentWinners?.Release();
        childBuffer?.Release();
        childFitnessBuffer?.Release();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }
}