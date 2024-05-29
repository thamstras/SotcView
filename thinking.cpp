
struct WorkItem
{
    uint32_t id;
    func<void> exec;
    func<void> fin;
    condition_variable done;
};

struct Worker
{
    jthread thread;
    condition_variable working;
    WorkItem *currentItem;
};

struct WorkPool
{
    queue<WorkItem> work;
    vector<Worker> workers;

    void Add(WorkItem);
    void UpdateWorkers();
};