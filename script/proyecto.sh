BASE_DIR=$(dirname "$PWD")


run() {
    # run the project

    # reuse the main script
    make --directory "$BASE_DIR" dev
    return 0;
}

clean() {
    # delete unnecesary (not tracked) files
    
    # this is simple
    # since rm doesn't delete hidden directories and .git is (indeed) a hidden directory
    rm -rf $BASE_DIR
    git reset --hard
    echo "the purge was a success"
    return 0;
}

report() {
    # delete unnecesary (not tracked) files
    return 0;
}

slides() {
    # delete unnecesary (not tracked) files
    return 0;
}

show_report() {
    # 

    return 0;
}

show_slides() {
    # 
    return 0;
}

# what's this, you ask?
# I just named my functions as the commands so I don't have to make a switch
$1

if [ $? != 0 ]
then
    compgen -A function;
fi
